(() => {
    "use strict";

    const app = document.getElementById("faceApp");
    if (!app) return;

    const mode = app.dataset.mode;
    const returnUrl = app.dataset.returnUrl;
    const autoStart = app.dataset.autoStart === "true";

    const video = document.getElementById("faceVideo");
    const canvas = document.getElementById("captureCanvas");
    const status = document.getElementById("faceStatus");
    const progress = document.getElementById("faceProgress");
    const progressText = document.getElementById("progressText");
    const startButton = document.getElementById("startFaceButton");
    const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const HOLD_MS = 3000;
    const LOOP_DELAY_MS = 180;
    const VERIFY_DELAY_MS = 650;
    const MAX_QUALITY_PROGRESS = 90;

    let stream = null;
    let running = false;
    let localProgress = 0;
    let holdStart = null;
    let lastLoopTime = 0;
    let lastVerifySentAt = 0;
    let previousSample = null;

    let faceLandmarker = null;
    let handLandmarker = null;
    let visionLoaded = false;

    function sleep(ms) {
        return new Promise(resolve => window.setTimeout(resolve, ms));
    }

    function setProgress(value, isError = false) {
        const safeValue = Math.round(Math.max(0, Math.min(100, Number(value) || 0)));
        localProgress = safeValue;

        progress.style.width = `${safeValue}%`;
        progress.classList.toggle("danger", isError);

        if (progressText) {
            progressText.textContent = `${safeValue}%`;
        }
    }

    function reduceProgress(amount, message) {
        holdStart = null;
        setProgress(Math.max(0, localProgress - amount), true);
        setStatus(message, "error");
    }

    function resetProgress(message) {
        holdStart = null;
        setProgress(0, true);
        setStatus(message, "error");
    }

    function setStatus(message, type = "normal") {
        status.textContent = message;
        status.className = `face-status ${type}`;
    }

    async function loadVisionModels() {
        if (visionLoaded && faceLandmarker && handLandmarker) {
            return;
        }

        setStatus("Loading face and hand detection models…", "working");

        if (!window.mpVisionReady) {
            throw new Error(
                "MediaPipe loader is missing. Check Register.cshtml or Verify.cshtml."
            );
        }

        const vision = await Promise.race([
            window.mpVisionReady,
            new Promise((_, reject) =>
                window.setTimeout(
                    () => reject(
                        new Error(
                            "MediaPipe took too long to load. Check your internet connection and CDN access."
                        )
                    ),
                    15000
                )
            )
        ]);

        if (!vision?.FilesetResolver ||
            !vision?.FaceLandmarker ||
            !vision?.HandLandmarker) {
            throw new Error(
                "MediaPipe loaded, but the required FaceLandmarker or HandLandmarker classes are unavailable."
            );
        }

        const filesetResolver = await vision.FilesetResolver.forVisionTasks(
            "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm"
        );

        try {
            faceLandmarker = await vision.FaceLandmarker.createFromOptions(
                filesetResolver,
                {
                    baseOptions: {
                        modelAssetPath:
                            "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task",
                        delegate: "GPU"
                    },
                    runningMode: "VIDEO",
                    numFaces: 2,
                    minFaceDetectionConfidence: 0.6,
                    minFacePresenceConfidence: 0.6,
                    minTrackingConfidence: 0.6,
                    outputFaceBlendshapes: false,
                    outputFacialTransformationMatrixes: false
                }
            );

            handLandmarker = await vision.HandLandmarker.createFromOptions(
                filesetResolver,
                {
                    baseOptions: {
                        modelAssetPath:
                            "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task",
                        delegate: "GPU"
                    },
                    runningMode: "VIDEO",
                    numHands: 2,
                    minHandDetectionConfidence: 0.55,
                    minHandPresenceConfidence: 0.55,
                    minTrackingConfidence: 0.55
                }
            );
        } catch (gpuError) {
            console.warn(
                "MediaPipe GPU initialization failed. Retrying with CPU.",
                gpuError
            );

            faceLandmarker = await vision.FaceLandmarker.createFromOptions(
                filesetResolver,
                {
                    baseOptions: {
                        modelAssetPath:
                            "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task",
                        delegate: "CPU"
                    },
                    runningMode: "VIDEO",
                    numFaces: 2,
                    minFaceDetectionConfidence: 0.6,
                    minFacePresenceConfidence: 0.6,
                    minTrackingConfidence: 0.6,
                    outputFaceBlendshapes: false,
                    outputFacialTransformationMatrixes: false
                }
            );

            handLandmarker = await vision.HandLandmarker.createFromOptions(
                filesetResolver,
                {
                    baseOptions: {
                        modelAssetPath:
                            "https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task",
                        delegate: "CPU"
                    },
                    runningMode: "VIDEO",
                    numHands: 2,
                    minHandDetectionConfidence: 0.55,
                    minHandPresenceConfidence: 0.55,
                    minTrackingConfidence: 0.55
                }
            );
        }

        visionLoaded = true;
    }

    async function startCamera() {
        if (!navigator.mediaDevices?.getUserMedia) {
            throw new Error("Camera access is not supported by this browser.");
        }

        stream = await navigator.mediaDevices.getUserMedia({
            video: {
                facingMode: "user",
                width: { ideal: 640 },
                height: { ideal: 480 }
            },
            audio: false
        });

        video.srcObject = stream;
        await video.play();

        if (!video.videoWidth || !video.videoHeight) {
            await new Promise(resolve => {
                video.addEventListener("loadeddata", resolve, { once: true });
            });
        }
    }

    function stopCamera() {
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        video.srcObject = null;
    }

    function captureFrame() {
        const width = 480;
        const ratio = video.videoWidth > 0
            ? video.videoHeight / video.videoWidth
            : 0.75;
        const height = Math.round(width * ratio);

        canvas.width = width;
        canvas.height = height;

        const context = canvas.getContext("2d", { willReadFrequently: true });
        context.drawImage(video, 0, 0, width, height);

        return {
            imageDataUrl: canvas.toDataURL("image/jpeg", 0.84),
            isStable: calculateStability(context, width, height)
        };
    }

    function calculateStability(context, width, height) {
        const sampleWidth = 32;
        const sampleHeight = 24;
        const temporary = document.createElement("canvas");

        temporary.width = sampleWidth;
        temporary.height = sampleHeight;

        const temporaryContext = temporary.getContext("2d", {
            willReadFrequently: true
        });

        temporaryContext.drawImage(
            canvas,
            0,
            0,
            width,
            height,
            0,
            0,
            sampleWidth,
            sampleHeight
        );

        const pixels = temporaryContext
            .getImageData(0, 0, sampleWidth, sampleHeight)
            .data;

        const sample = new Uint8Array(sampleWidth * sampleHeight);

        for (let i = 0, j = 0; i < pixels.length; i += 4, j++) {
            sample[j] = Math.round(
                pixels[i] * 0.299 +
                pixels[i + 1] * 0.587 +
                pixels[i + 2] * 0.114
            );
        }

        if (!previousSample) {
            previousSample = sample;
            return true;
        }

        let totalDifference = 0;

        for (let index = 0; index < sample.length; index++) {
            totalDifference += Math.abs(
                sample[index] - previousSample[index]
            );
        }

        previousSample = sample;

        const averageDifference = totalDifference / sample.length;
        return averageDifference < 16;
    }

    function getBoundingBox(landmarks) {
        let minX = 1;
        let minY = 1;
        let maxX = 0;
        let maxY = 0;

        for (const point of landmarks) {
            minX = Math.min(minX, point.x);
            minY = Math.min(minY, point.y);
            maxX = Math.max(maxX, point.x);
            maxY = Math.max(maxY, point.y);
        }

        return {
            minX,
            minY,
            maxX,
            maxY,
            width: maxX - minX,
            height: maxY - minY,
            centerX: (minX + maxX) / 2,
            centerY: (minY + maxY) / 2
        };
    }

    function pointInsideFace(point, box) {
        const paddingX = Math.max(0.025, box.width * 0.08);
        const paddingY = Math.max(0.025, box.height * 0.08);

        return point.x >= box.minX - paddingX &&
            point.x <= box.maxX + paddingX &&
            point.y >= box.minY - paddingY &&
            point.y <= box.maxY + paddingY;
    }

    function isHandCoveringFace(handResults, faceBox) {
        const hands = handResults?.landmarks ?? [];

        for (const hand of hands) {
            let overlapPoints = 0;

            for (const point of hand) {
                if (pointInsideFace(point, faceBox)) {
                    overlapPoints++;
                }
            }

            if (overlapPoints >= 2) {
                return true;
            }
        }

        return false;
    }

    function analyseFaceQuality(now) {
        if (!faceLandmarker || !handLandmarker) {
            return {
                valid: false,
                level: "model",
                message: "Face detection models are still loading."
            };
        }

        const faceResults = faceLandmarker.detectForVideo(video, now);
        const handResults = handLandmarker.detectForVideo(video, now);
        const faces = faceResults?.faceLandmarks ?? [];

        if (faces.length === 0) {
            return {
                valid: false,
                level: "no-face",
                message: "No face detected. Please look directly at the camera."
            };
        }

        if (faces.length > 1) {
            return {
                valid: false,
                level: "multiple",
                message: "Multiple faces detected. Only one person is allowed."
            };
        }

        const landmarks = faces[0];
        const faceBox = getBoundingBox(landmarks);

        if (isHandCoveringFace(handResults, faceBox)) {
            return {
                valid: false,
                level: "covered",
                message: "Face is covered by a hand. Please remove your hand and keep your full face visible."
            };
        }

        const centered =
            faceBox.centerX >= 0.37 &&
            faceBox.centerX <= 0.63 &&
            faceBox.centerY >= 0.35 &&
            faceBox.centerY <= 0.65;

        if (!centered) {
            return {
                valid: false,
                level: "position",
                message: "Please move your face to the centre of the guide."
            };
        }

        if (faceBox.width < 0.20 || faceBox.height < 0.25) {
            return {
                valid: false,
                level: "too-far",
                message: "Your face is too far away. Please move closer to the camera."
            };
        }

        if (faceBox.width > 0.72 || faceBox.height > 0.82) {
            return {
                valid: false,
                level: "too-close",
                message: "Your face is too close. Please move slightly away from the camera."
            };
        }

        const frame = captureFrame();

        if (!frame.isStable) {
            return {
                valid: false,
                level: "movement",
                message: "Movement detected. Please keep your face still."
            };
        }

        return {
            valid: true,
            level: "good",
            message: "Face detected. Keep still and keep your full face visible.",
            frame
        };
    }

    function handleInvalidQuality(result) {
        switch (result.level) {
            case "no-face":
            case "multiple":
                resetProgress(result.message);
                break;

            case "covered":
                reduceProgress(45, result.message);
                break;

            case "movement":
                reduceProgress(25, result.message);
                break;

            case "position":
            case "too-far":
            case "too-close":
                reduceProgress(20, result.message);
                break;

            default:
                reduceProgress(15, result.message);
                break;
        }
    }

    async function sendFrame(endpoint, frame) {
        const response = await fetch(endpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token
            },
            body: JSON.stringify({
                imageDataUrl: frame.imageDataUrl,
                isStable: frame.isStable
            })
        });

        const data = await response.json().catch(() => ({
            success: false,
            message: "The server returned an invalid response."
        }));

        return { response, data };
    }

    function updateQualityProgress(now) {
        if (!holdStart) {
            holdStart = now;
        }

        const elapsed = Math.max(0, now - holdStart);
        const ratio = Math.min(1, elapsed / HOLD_MS);
        const target = 20 + ratio * 70;

        setProgress(Math.max(localProgress, target), false);
    }

    async function submitRegistration(frame) {
        setProgress(92, false);
        setStatus("Face quality passed. Registering your face…", "working");

        const { response, data } = await sendFrame(
            "/FaceId/RegisterFrame",
            frame
        );

        if (!response.ok || !data.success) {
            holdStart = null;
            setProgress(25, true);
            setStatus(
                data.message || "Face registration failed. Please try again.",
                "error"
            );
            return false;
        }

        setProgress(100, false);
        setStatus(
            data.message || "Face registration completed successfully.",
            "success"
        );

        running = false;
        stopCamera();
        window.setTimeout(() => window.location.assign(returnUrl), 900);
        return true;
    }

    async function submitVerification(frame) {
        const now = performance.now();

        if (now - lastVerifySentAt < VERIFY_DELAY_MS) {
            return false;
        }

        lastVerifySentAt = now;

        const { response, data } = await sendFrame(
            "/FaceId/VerifyFrame",
            frame
        );

        const serverProgress = Math.max(
            0,
            Math.min(100, Number(data.progress ?? 0))
        );

        if (!response.ok || !data.success) {
            holdStart = null;
            setProgress(Math.max(20, localProgress - 35), true);
            setStatus(
                data.message || "Verification interrupted. Please try again.",
                "error"
            );
            return false;
        }

        const verificationProgress =
            MAX_QUALITY_PROGRESS + Math.round(serverProgress * 0.09);

        setProgress(
            data.verified ? 100 : Math.min(99, verificationProgress),
            false
        );

        const confidence = data.similarity !== undefined && data.similarity !== null
            ? ` AI match: ${Number(data.similarity).toFixed(1)}%.`
            : "";

        setStatus(
            `${data.message || "Identity comparison in progress."}${confidence}`,
            data.verified ? "success" : "working"
        );

        if (data.verified) {
            running = false;
            stopCamera();
            window.setTimeout(() => window.location.assign(returnUrl), 800);
            return true;
        }

        return false;
    }

    async function runFaceLoop() {
        while (running) {
            const now = performance.now();

            if (now - lastLoopTime < LOOP_DELAY_MS) {
                await sleep(40);
                continue;
            }

            lastLoopTime = now;

            let quality;

            try {
                quality = analyseFaceQuality(now);
            } catch (error) {
                console.error("Face quality analysis failed:", error);
                reduceProgress(
                    20,
                    "Face analysis was interrupted. Please keep your face visible and try again."
                );
                await sleep(LOOP_DELAY_MS);
                continue;
            }

            if (!quality.valid) {
                handleInvalidQuality(quality);
                await sleep(LOOP_DELAY_MS);
                continue;
            }

            updateQualityProgress(now);
            setStatus(quality.message, "working");

            if (localProgress < MAX_QUALITY_PROGRESS) {
                await sleep(LOOP_DELAY_MS);
                continue;
            }

            if (mode === "register") {
                const completed = await submitRegistration(quality.frame);

                if (completed) {
                    return;
                }
            } else {
                const completed = await submitVerification(quality.frame);

                if (completed) {
                    return;
                }
            }

            await sleep(LOOP_DELAY_MS);
        }
    }

    async function beginFaceProcess() {
        if (running) return;

        running = true;
        startButton.disabled = true;
        startButton.textContent = mode === "register"
            ? "Registration Running…"
            : "Verification Running…";

        holdStart = null;
        lastLoopTime = 0;
        lastVerifySentAt = 0;
        previousSample = null;
        setProgress(0, false);
        setStatus("Requesting camera permission…", "working");

        try {
            await loadVisionModels();

            if (!stream) {
                await startCamera();
            }

            setStatus(
                "Camera ready. Keep one full face inside the guide.",
                "working"
            );

            await runFaceLoop();
        } catch (error) {
            console.error("Face process failed:", error);
            running = false;
            stopCamera();
            setProgress(0, true);
            setStatus(
                error?.name === "NotAllowedError"
                    ? "Camera access was denied. Please allow camera permission and try again."
                    : error?.message || "Unable to start Face ID.",
                "error"
            );
        } finally {
            if (!running) {
                startButton.disabled = false;
                startButton.textContent = mode === "register"
                    ? "Restart Registration"
                    : "Restart Verification";
            }
        }
    }

    startButton.addEventListener("click", () => {
        if (running) {
            return;
        }

        beginFaceProcess();
    });

    if (autoStart) {
        window.setTimeout(beginFaceProcess, 250);
    }

    window.addEventListener("beforeunload", () => {
        running = false;
        stopCamera();
    });
})();
