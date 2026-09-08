(() => {
    "use strict";

    const modal = document.getElementById("photoStudioModal");
    const openButton = document.getElementById("openPhotoStudioButton");
    if (!modal || !openButton) return;

    const canvas = document.getElementById("photoEditorCanvas");
    const ctx = canvas.getContext("2d", { alpha: false });
    const shell = document.getElementById("photoCanvasShell");
    const emptyState = document.getElementById("photoStudioEmpty");
    const fileInput = document.getElementById("photoStudioFileInput");
    const uploadButton = document.getElementById("uploadPhotoButton");
    const takePhotoButton = document.getElementById("takePhotoButton");
    const saveButton = document.getElementById("savePhotoButton");
    const resetButton = document.getElementById("resetPhotoButton");
    const uploadForm = document.getElementById("photoStudioUploadForm");
    const processedInput = document.getElementById("processedAvatarInput");
    const autoEnhanceButton = document.getElementById("autoEnhanceButton");
    const removeBackgroundButton = document.getElementById("removeBackgroundButton");
    const aiStatus = document.getElementById("photoAiStatus");
    const currentAvatarUrl = modal.dataset.currentAvatarUrl || "";
    const avatarInitial = modal.dataset.avatarInitial || "U";

    const zoomSlider = document.getElementById("zoomSlider");
    const brightnessSlider = document.getElementById("brightnessSlider");
    const contrastSlider = document.getElementById("contrastSlider");
    const saturationSlider = document.getElementById("saturationSlider");
    const zoomValue = document.getElementById("zoomValue");
    const brightnessValue = document.getElementById("brightnessValue");
    const contrastValue = document.getElementById("contrastValue");
    const saturationValue = document.getElementById("saturationValue");
    const rotateLeftButton = document.getElementById("rotateLeftButton");
    const flipButton = document.getElementById("flipButton");

    const cameraModal = document.getElementById("cameraModal");
    const cameraVideo = document.getElementById("cameraVideo");
    const cameraMessage = document.getElementById("cameraMessage");
    const closeCameraButton = document.getElementById("closeCameraButton");
    const cancelCameraButton = document.getElementById("cancelCameraButton");
    const capturePhotoButton = document.getElementById("capturePhotoButton");

    const state = {
        image: null,
        originalSource: null,
        zoom: 1,
        rotation: 0,
        flipX: 1,
        brightness: 100,
        contrast: 100,
        saturation: 100,
        filter: "original",
        offsetX: 0,
        offsetY: 0,
        dragging: false,
        dragStartX: 0,
        dragStartY: 0,
        startOffsetX: 0,
        startOffsetY: 0,
        stream: null
    };

    function openStudio() {
        if (!state.image) loadStartingAvatar();
        modal.classList.add("open");
        modal.setAttribute("aria-hidden", "false");
        document.body.classList.add("photo-studio-open");
    }

    function initialAvatarDataUrl() {
        const safeInitial = avatarInitial.replace(/[<>&"']/g, "");
        const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="640"><rect width="100%" height="100%" fill="#0f7f99"/><text x="50%" y="53%" text-anchor="middle" dominant-baseline="middle" font-family="Arial,sans-serif" font-size="260" font-weight="700" fill="white">${safeInitial}</text></svg>`;
        return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
    }

    function loadStartingAvatar() {
        loadImageFromUrl(currentAvatarUrl || initialAvatarDataUrl(), true);
    }

    function closeStudio() {
        modal.classList.remove("open");
        modal.setAttribute("aria-hidden", "true");
        document.body.classList.remove("photo-studio-open");
        stopCamera();
    }

    function resetAdjustments() {
        state.zoom = 1;
        state.rotation = 0;
        state.flipX = 1;
        state.brightness = 100;
        state.contrast = 100;
        state.saturation = 100;
        state.filter = "original";
        state.offsetX = 0;
        state.offsetY = 0;

        zoomSlider.value = "100";
        brightnessSlider.value = "100";
        contrastSlider.value = "100";
        saturationSlider.value = "100";
        updateSliderLabels();

        document.querySelectorAll(".photo-filter").forEach(button => {
            button.classList.toggle("active", button.dataset.filter === "original");
        });
    }

    function updateSliderLabels() {
        zoomValue.value = `${zoomSlider.value}%`;
        brightnessValue.value = `${brightnessSlider.value}%`;
        contrastValue.value = `${contrastSlider.value}%`;
        saturationValue.value = `${saturationSlider.value}%`;
    }

    function filterString() {
        let brightness = state.brightness;
        let contrast = state.contrast;
        let saturation = state.saturation;
        let sepia = 0;
        let hue = 0;
        let grayscale = 0;

        switch (state.filter) {
            case "warm":
                sepia = 18;
                saturation *= 1.08;
                brightness *= 1.02;
                break;
            case "cool":
                hue = 8;
                saturation *= 0.92;
                contrast *= 1.03;
                break;
            case "vivid":
                saturation *= 1.25;
                contrast *= 1.08;
                break;
            case "bw":
                grayscale = 100;
                break;
        }

        return `brightness(${brightness}%) contrast(${contrast}%) saturate(${saturation}%) sepia(${sepia}%) hue-rotate(${hue}deg) grayscale(${grayscale}%)`;
    }

    function draw() {
        ctx.save();
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = "#eef2f5";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        if (!state.image) {
            ctx.restore();
            emptyState.classList.remove("hidden");
            saveButton.disabled = true;
            return;
        }

        emptyState.classList.add("hidden");
        saveButton.disabled = false;

        const rotated = Math.abs(state.rotation % 180) === 90;
        const sourceWidth = rotated ? state.image.height : state.image.width;
        const sourceHeight = rotated ? state.image.width : state.image.height;
        const baseScale = Math.max(canvas.width / sourceWidth, canvas.height / sourceHeight);
        const scale = baseScale * state.zoom;

        ctx.translate(canvas.width / 2 + state.offsetX, canvas.height / 2 + state.offsetY);
        ctx.rotate(state.rotation * Math.PI / 180);
        ctx.scale(state.flipX, 1);
        ctx.filter = filterString();
        ctx.drawImage(
            state.image,
            -state.image.width * scale / 2,
            -state.image.height * scale / 2,
            state.image.width * scale,
            state.image.height * scale
        );
        ctx.restore();
    }

    function loadImageFromUrl(url, startingAvatar = false) {
        const image = new Image();
        image.onload = () => {
            state.image = image;
            state.originalSource = url;
            resetAdjustments();
            draw();
        };
        image.onerror = () => {
            if (startingAvatar && url !== initialAvatarDataUrl()) {
                loadImageFromUrl(initialAvatarDataUrl(), true);
                return;
            }
            alert("The selected image could not be opened.");
        };
        image.src = url;
    }

    function validateAndLoadFile(file) {
        if (!file) return;

        const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
        if (!allowedTypes.includes(file.type)) {
            alert("Only JPG, JPEG, PNG and WEBP images are allowed.");
            fileInput.value = "";
            return;
        }

        if (file.size > 8 * 1024 * 1024) {
            alert("Please choose an image smaller than 8MB. The final saved avatar will be compressed before upload.");
            fileInput.value = "";
            return;
        }

        const reader = new FileReader();
        reader.onload = event => loadImageFromUrl(event.target.result);
        reader.readAsDataURL(file);
    }

    async function openCamera() {
        cameraMessage.classList.remove("error");
        cameraMessage.textContent = "Allow camera access when your browser asks for permission.";
        cameraModal.classList.add("open");
        cameraModal.setAttribute("aria-hidden", "false");

        try {
            state.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: "user", width: { ideal: 1280 }, height: { ideal: 960 } },
                audio: false
            });
            cameraVideo.srcObject = state.stream;
        } catch (error) {
            cameraMessage.classList.add("error");
            cameraMessage.textContent = "Camera access was blocked or is unavailable. You can still upload a photo instead.";
        }
    }

    function stopCamera() {
        if (state.stream) {
            state.stream.getTracks().forEach(track => track.stop());
            state.stream = null;
        }
        cameraVideo.srcObject = null;
        cameraModal.classList.remove("open");
        cameraModal.setAttribute("aria-hidden", "true");
    }

    function capturePhoto() {
        if (!cameraVideo.videoWidth || !cameraVideo.videoHeight) {
            cameraMessage.classList.add("error");
            cameraMessage.textContent = "The camera is not ready yet.";
            return;
        }

        const captureCanvas = document.createElement("canvas");
        captureCanvas.width = cameraVideo.videoWidth;
        captureCanvas.height = cameraVideo.videoHeight;
        const captureContext = captureCanvas.getContext("2d");

        captureContext.translate(captureCanvas.width, 0);
        captureContext.scale(-1, 1);
        captureContext.drawImage(cameraVideo, 0, 0);

        const dataUrl = captureCanvas.toDataURL("image/jpeg", 0.94);
        stopCamera();
        loadImageFromUrl(dataUrl);
    }

    function clientPoint(event) {
        if (event.touches && event.touches.length) {
            return { x: event.touches[0].clientX, y: event.touches[0].clientY };
        }
        return { x: event.clientX, y: event.clientY };
    }

    function startDrag(event) {
        if (!state.image) return;
        const point = clientPoint(event);
        state.dragging = true;
        state.dragStartX = point.x;
        state.dragStartY = point.y;
        state.startOffsetX = state.offsetX;
        state.startOffsetY = state.offsetY;
        shell.classList.add("dragging");
        if (event.pointerId !== undefined && shell.setPointerCapture) shell.setPointerCapture(event.pointerId);
    }

    function moveDrag(event) {
        if (!state.dragging) return;
        const point = clientPoint(event);
        const rect = shell.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        state.offsetX = state.startOffsetX + (point.x - state.dragStartX) * scaleX;
        state.offsetY = state.startOffsetY + (point.y - state.dragStartY) * scaleY;
        draw();
    }

    function endDrag() {
        state.dragging = false;
        shell.classList.remove("dragging");
    }

    function currentCanvasBlob() {
        return new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.92));
    }

    async function processWithCloudinary(operation, button, workingText) {
        if (!state.image) return;

        const blob = await currentCanvasBlob();
        if (!blob) {
            alert("Could not prepare the photo for Cloudinary.");
            return;
        }

        const token = uploadForm.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const data = new FormData();
        data.append("photo", new File([blob], `cloudinary-${Date.now()}.jpg`, { type: "image/jpeg" }));
        data.append("operation", operation);
        if (token) data.append("__RequestVerificationToken", token);

        const originalText = button.textContent;
        autoEnhanceButton.disabled = true;
        removeBackgroundButton.disabled = true;
        button.textContent = workingText;
        aiStatus.classList.remove("error", "success");
        aiStatus.textContent = "Processing with Cloudinary...";

        try {
            const response = await fetch("/MyProfile/ProcessPhotoWithCloudinary", {
                method: "POST",
                body: data
            });

            if (!response.ok) {
                let message = "Cloudinary could not process the photo.";
                try {
                    const error = await response.json();
                    if (error.message) message = error.message;
                } catch { }
                throw new Error(message);
            }

            const resultBlob = await response.blob();
            const objectUrl = URL.createObjectURL(resultBlob);
            const image = new Image();
            image.onload = () => {
                state.image = image;
                state.originalSource = objectUrl;
                resetAdjustments();
                draw();
                aiStatus.classList.add("success");
                aiStatus.textContent = operation === "enhance"
                    ? "Auto Enhance applied."
                    : "Background removed.";
            };
            image.onerror = () => {
                URL.revokeObjectURL(objectUrl);
                throw new Error("The processed Cloudinary image could not be opened.");
            };
            image.src = objectUrl;
        } catch (error) {
            aiStatus.classList.add("error");
            aiStatus.textContent = error.message;
        } finally {
            autoEnhanceButton.disabled = false;
            removeBackgroundButton.disabled = false;
            button.textContent = originalText;
        }
    }

    function exportAvatar() {
        if (!state.image || saveButton.disabled) return;

        saveButton.disabled = true;
        saveButton.textContent = "Saving...";

        const output = document.createElement("canvas");
        output.width = 640;
        output.height = 640;
        const outputContext = output.getContext("2d", { alpha: false });
        outputContext.drawImage(canvas, 0, 0, output.width, output.height);

        output.toBlob(blob => {
            if (!blob) {
                saveButton.disabled = false;
                saveButton.textContent = "Save Profile Photo";
                alert("Could not prepare the profile photo. Please try again.");
                return;
            }

            const file = new File([blob], `profile-${Date.now()}.jpg`, { type: "image/jpeg" });
            const transfer = new DataTransfer();
            transfer.items.add(file);
            processedInput.files = transfer.files;
            uploadForm.submit();
        }, "image/jpeg", 0.9);
    }

    openButton.addEventListener("click", openStudio);
    modal.querySelectorAll("[data-close-photo-studio]").forEach(button => button.addEventListener("click", closeStudio));
    uploadButton.addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", event => validateAndLoadFile(event.target.files[0]));
    takePhotoButton.addEventListener("click", openCamera);
    closeCameraButton.addEventListener("click", stopCamera);
    cancelCameraButton.addEventListener("click", stopCamera);
    capturePhotoButton.addEventListener("click", capturePhoto);
    saveButton.addEventListener("click", exportAvatar);
    autoEnhanceButton.addEventListener("click", () => processWithCloudinary("enhance", autoEnhanceButton, "✨ Enhancing..."));
    removeBackgroundButton.addEventListener("click", () => processWithCloudinary("remove-background", removeBackgroundButton, "👤 Removing..."));

    resetButton.addEventListener("click", () => {
        if (!state.image) return;
        resetAdjustments();
        draw();
    });

    rotateLeftButton.addEventListener("click", () => {
        if (!state.image) return;
        state.rotation = (state.rotation - 90) % 360;
        draw();
    });

    flipButton.addEventListener("click", () => {
        if (!state.image) return;
        state.flipX *= -1;
        draw();
    });

    [
        [zoomSlider, "zoom"],
        [brightnessSlider, "brightness"],
        [contrastSlider, "contrast"],
        [saturationSlider, "saturation"]
    ].forEach(([slider, key]) => {
        slider.addEventListener("input", () => {
            if (key === "zoom") state.zoom = Number(slider.value) / 100;
            else state[key] = Number(slider.value);
            updateSliderLabels();
            draw();
        });
    });

    document.querySelectorAll(".photo-filter").forEach(button => {
        button.addEventListener("click", () => {
            if (!state.image) return;
            state.filter = button.dataset.filter;
            document.querySelectorAll(".photo-filter").forEach(item => item.classList.remove("active"));
            button.classList.add("active");
            draw();
        });
    });

    shell.addEventListener("pointerdown", startDrag);
    shell.addEventListener("pointermove", moveDrag);
    shell.addEventListener("pointerup", endDrag);
    shell.addEventListener("pointercancel", endDrag);
    shell.addEventListener("pointerleave", event => {
        if (event.buttons === 0) endDrag();
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") return;
        if (cameraModal.classList.contains("open")) stopCamera();
        else if (modal.classList.contains("open")) closeStudio();
    });

    updateSliderLabels();
    loadStartingAvatar();
})();
