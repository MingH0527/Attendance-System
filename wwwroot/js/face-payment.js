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

    let stream = null;
    let running = false;
    let previousSample = null;
    let localProgress = 0;

    function setProgress(value, isError = false) {
        const safeValue = Math.max(0, Math.min(100, value));
        progress.style.width = `${safeValue}%`;
        progress.classList.toggle("danger", isError);
        if (progressText) progressText.textContent = `${safeValue}%`;
    }

    function setStatus(message, type = "normal") {
        status.textContent = message;
        status.className = `face-status ${type}`;
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
    }

    function stopCamera() {
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
    }

    function captureFrame() {
        const width = 480;
        const height = Math.round(width * (video.videoHeight / video.videoWidth || 0.75));
        canvas.width = width;
        canvas.height = height;

        const context = canvas.getContext("2d", { willReadFrequently: true });
        context.drawImage(video, 0, 0, width, height);

        return {
            imageDataUrl: canvas.toDataURL("image/jpeg", 0.82),
            isStable: calculateStability(context, width, height)
        };
    }

    function calculateStability(context, width, height) {
        const sampleWidth = 32;
        const sampleHeight = 24;
        const temporary = document.createElement("canvas");
        temporary.width = sampleWidth;
        temporary.height = sampleHeight;
        const temporaryContext = temporary.getContext("2d", { willReadFrequently: true });
        temporaryContext.drawImage(canvas, 0, 0, width, height, 0, 0, sampleWidth, sampleHeight);

        const pixels = temporaryContext.getImageData(0, 0, sampleWidth, sampleHeight).data;
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
            totalDifference += Math.abs(sample[index] - previousSample[index]);
        }

        previousSample = sample;
        const averageDifference = totalDifference / sample.length;
        return averageDifference < 18;
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

    async function runRegistration() {
        setStatus("Hold still while AI analyses your face…", "working");
        setProgress(25);
        await new Promise(resolve => setTimeout(resolve, 800));

        const frame = captureFrame();
        if (!frame.isStable) {
            setProgress(5, true);
            setStatus("Too much movement detected. Please keep still and try again.", "error");
            running = false;
            startButton.disabled = false;
            startButton.textContent = "Try Again";
            return;
        }

        setProgress(55);
        const { response, data } = await sendFrame("/FaceId/RegisterFrame", frame);

        if (!response.ok || !data.success) {
            setProgress(15, true);
            setStatus(data.message || "Face registration failed.", "error");
            running = false;
            startButton.disabled = false;
            startButton.textContent = "Try Again";
            return;
        }

        setProgress(100);
        setStatus(data.message, "success");
        stopCamera();
        setTimeout(() => window.location.assign(returnUrl), 900);
    }

    async function runVerification() {
        while (running) {
            const frame = captureFrame();

            if (!frame.isStable) {
                localProgress = Math.max(0, localProgress - 30);
                setProgress(localProgress, true);
                setStatus("Movement detected. Keep your face still.", "error");
                await new Promise(resolve => setTimeout(resolve, 650));
                continue;
            }

            const { response, data } = await sendFrame("/FaceId/VerifyFrame", frame);
            localProgress = Number(data.progress ?? 0);

            if (!response.ok || !data.success) {
                setProgress(localProgress, true);
                setStatus(data.message || "Verification interrupted.", "error");
                await new Promise(resolve => setTimeout(resolve, 700));
                continue;
            }

            setProgress(localProgress, false);
            const confidence = data.similarity ? ` AI match: ${data.similarity}%.` : "";
            setStatus(`${data.message}${confidence}`, data.verified ? "success" : "working");

            if (data.verified) {
                running = false;
                stopCamera();
                setTimeout(() => window.location.assign(returnUrl), 800);
                return;
            }

            await new Promise(resolve => setTimeout(resolve, 700));
        }
    }

    async function beginFaceProcess() {
        if (running) return;

        running = true;
        startButton.disabled = true;
        setProgress(0);
        setStatus("Requesting camera permission…", "working");

        try {
            if (!stream) await startCamera();
            previousSample = null;

            if (mode === "register") {
                await runRegistration();
            } else {
                await runVerification();
            }
        } catch (error) {
            running = false;
            startButton.disabled = false;
            setProgress(0, true);
            setStatus(error.message || "Unable to open the camera.", "error");
        }
    }

    startButton.addEventListener("click", beginFaceProcess);

    if (autoStart) {
        window.setTimeout(beginFaceProcess, 250);
    }

    window.addEventListener("beforeunload", stopCamera);
})();
