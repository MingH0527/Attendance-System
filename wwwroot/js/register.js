(() => {
    "use strict";

    const form =
        document.getElementById("multiStepForm");

    if (!form) {
        return;
    }

    // ==============================
    // Form steps
    // ==============================

    const steps = [
        ...document.querySelectorAll(".form-step")
    ];

    const nodes = [
        ...document.querySelectorAll(".step-node")
    ];

    const title =
        document.getElementById("stepTitle");

    const count =
        document.getElementById("stepCount");

    const progressFill =
        document.getElementById("progressFill");

    const titles = [
        "Profile details",
        "Location details",
        "Account security"
    ];

    let currentStep =
        Number(window.registerInitialStep || 1);

    // ==============================
    // Inputs
    // ==============================

    const username =
        document.getElementById("usernameInput");

    const email =
        document.getElementById("emailInput");

    const phone =
        document.getElementById("phoneInput");

    const address =
        document.getElementById("addressInput");

    const latitude =
        document.getElementById("latitudeInput");

    const longitude =
        document.getElementById("longitudeInput");

    const locationError =
        document.getElementById("clientLocationError");

    const password =
        document.getElementById("passwordInput");

    const confirmPassword =
        document.getElementById("confirmPasswordInput");

    const confirmError =
        document.getElementById("confirmClientError");

    const submitButton =
        document.getElementById("submitButton");

    // ==============================
    // Buttons
    // ==============================

    const nextStep1Button =
        document.getElementById("nextStep1");

    const nextStep2Button =
        document.getElementById("nextStep2");

    const locateMeButton =
        document.getElementById("locateMeBtn");

    const allowLocationButton =
        document.getElementById(
            "allowLocationButton"
        );

    const skipLocationButton =
        document.getElementById(
            "skipLocationButton"
        );

    // ==============================
    // Location modal
    // ==============================

    const locationModal =
        document.getElementById(
            "locationModal"
        );

    function openLocationModal() {
        if (!locationModal) {
            return;
        }

        locationModal.hidden = false;
    }

    function closeLocationModal() {
        if (!locationModal) {
            return;
        }

        locationModal.hidden = true;
    }

    // Always hide modal when Register first loads
    closeLocationModal();

    // ==============================
    // Custom validation error
    // ==============================

    function setCustomError(input, message) {
        if (!input) {
            return;
        }

        input.setCustomValidity(
            message || ""
        );

        const field =
            input.closest(".field, fieldset");

        const error =
            field?.querySelector(".field-error");

        if (
            error &&
            !error.dataset.serverError
        ) {
            error.textContent =
                message || "";
        }
    }

    // ==============================
    // Show selected step
    // ==============================

    function showStep(number) {
        currentStep =
            Math.max(
                1,
                Math.min(3, number)
            );

        steps.forEach(step => {
            const stepNumber =
                Number(step.dataset.step);

            step.classList.toggle(
                "active",
                stepNumber === currentStep
            );
        });

        nodes.forEach((node, index) => {
            const nodeNumber =
                index + 1;

            node.classList.toggle(
                "active",
                nodeNumber === currentStep
            );

            node.classList.toggle(
                "complete",
                nodeNumber < currentStep
            );
        });

        if (title) {
            title.textContent =
                titles[currentStep - 1];
        }

        if (count) {
            count.textContent =
                `Step ${currentStep} of 3`;
        }

        if (progressFill) {
            progressFill.style.width =
                `${((currentStep - 1) / 2) * 100}%`;
        }

        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });

        // Leaflet map needs resizing after
        // its hidden step becomes visible
        if (currentStep === 2) {
            setTimeout(() => {
                map?.invalidateSize();
            }, 150);
        }
    }

    // ==============================
    // Step 1 validation
    // ==============================

    function validateStep1() {
        let valid = true;

        const gmailPattern =
            /^[a-zA-Z0-9._%+-]+@gmail\.com$/i;

        const phonePattern =
            /^(011\d{8}|01[2-9]\d{7})$/;

        setCustomError(username, "");
        setCustomError(email, "");
        setCustomError(phone, "");

        // Username
        if (!username?.value.trim()) {
            setCustomError(
                username,
                "Username is required."
            );

            valid = false;

        } else if (
            username.value.trim().length > 30
        ) {
            setCustomError(
                username,
                "Username cannot exceed 30 characters."
            );

            valid = false;
        }

        // Email
        if (!email?.value.trim()) {
            setCustomError(
                email,
                "Email is required."
            );

            valid = false;

        } else if (
            !gmailPattern.test(
                email.value.trim()
            )
        ) {
            setCustomError(
                email,
                "Use a valid Gmail address ending with @gmail.com."
            );

            valid = false;
        }

        // Phone
        if (!phone?.value.trim()) {
            setCustomError(
                phone,
                "Phone number is required."
            );

            valid = false;

        } else if (
            !phonePattern.test(
                phone.value.trim()
            )
        ) {
            setCustomError(
                phone,
                "Enter a valid Malaysian number. 012–019 uses 10 digits, while 011 uses 11 digits."
            );

            valid = false;
        }

        // Gender
        const gender =
            form.querySelector(
                'input[name="Gender"]:checked'
            );

        const genderError =
            document.querySelector(
                ".gender-field .field-error"
            );

        if (!gender) {
            if (genderError) {
                genderError.textContent =
                    "Please select a gender.";
            }

            valid = false;

        } else if (genderError) {
            genderError.textContent = "";
        }

        if (!valid) {
            form.querySelector(":invalid")
                ?.reportValidity();
        }

        return valid;
    }

    // ==============================
    // Step 2 validation
    // ==============================

    function validateStep2() {
        const hasAddress =
            Boolean(address?.value.trim());

        const hasLatitude =
            Boolean(latitude?.value);

        const hasLongitude =
            Boolean(longitude?.value);

        const valid =
            hasAddress &&
            hasLatitude &&
            hasLongitude;

        if (locationError) {
            locationError.textContent =
                valid
                    ? ""
                    : "Please choose a location on the map before continuing.";
        }

        return valid;
    }

    // ==============================
    // Password rules
    // ==============================

    const passwordRules = {
        length: value =>
            value.length >= 8 &&
            value.length <= 15,

        case: value =>
            /[a-z]/.test(value) &&
            /[A-Z]/.test(value),

        number: value =>
            /\d/.test(value),

        symbol: value =>
            /[@$!%*?&]/.test(value)
    };

    function updatePasswordRules() {
        const value =
            password?.value || "";

        Object.entries(passwordRules)
            .forEach(([name, test]) => {
                document.querySelector(
                    `[data-rule="${name}"]`
                )?.classList.toggle(
                    "valid",
                    test(value)
                );
            });

        const allValid =
            Object.values(passwordRules)
                .every(test => test(value));

        if (!value) {
            setCustomError(password, "");
            return false;
        }

        setCustomError(
            password,
            allValid
                ? ""
                : "Password does not meet all requirements."
        );

        return allValid;
    }

    // ==============================
    // Step 3 validation
    // ==============================

    function validateStep3() {
        let valid = true;

        const passwordValue =
            password?.value || "";

        const confirmValue =
            confirmPassword?.value || "";

        if (!passwordValue) {
            setCustomError(
                password,
                "Password is required."
            );

            valid = false;

        } else if (!updatePasswordRules()) {
            valid = false;
        }

        if (confirmError) {
            confirmError.textContent = "";
        }

        if (!confirmValue) {
            if (confirmError) {
                confirmError.textContent =
                    "Please confirm your password.";
            }

            valid = false;

        } else if (
            confirmValue !== passwordValue
        ) {
            if (confirmError) {
                confirmError.textContent =
                    "Passwords do not match.";
            }

            valid = false;
        }

        return valid;
    }

    // ==============================
    // Input events
    // ==============================

    phone?.addEventListener(
        "input",
        () => {
            phone.value =
                phone.value
                    .replace(/\D/g, "")
                    .slice(0, 11);

            setCustomError(phone, "");
        }
    );

    username?.addEventListener(
        "input",
        () => {
            setCustomError(username, "");
        }
    );

    email?.addEventListener(
        "input",
        () => {
            setCustomError(email, "");
        }
    );

    form.querySelectorAll(
        'input[name="Gender"]'
    ).forEach(input => {
        input.addEventListener(
            "change",
            () => {
                const genderError =
                    document.querySelector(
                        ".gender-field .field-error"
                    );

                if (genderError) {
                    genderError.textContent = "";
                }
            }
        );
    });

    password?.addEventListener(
        "input",
        () => {
            updatePasswordRules();

            if (
                confirmPassword?.value &&
                confirmPassword.value !==
                password.value
            ) {
                if (confirmError) {
                    confirmError.textContent =
                        "Passwords do not match.";
                }

            } else if (confirmError) {
                confirmError.textContent = "";
            }
        }
    );

    confirmPassword?.addEventListener(
        "input",
        () => {
            if (!confirmError) {
                return;
            }

            if (!confirmPassword.value) {
                confirmError.textContent = "";
                return;
            }

            confirmError.textContent =
                confirmPassword.value !==
                    password?.value
                    ? "Passwords do not match."
                    : "";
        }
    );

    // ==============================
    // Step navigation
    // ==============================

    nextStep1Button?.addEventListener(
        "click",
        () => {
            if (!validateStep1()) {
                return;
            }

            // Enter Step 2 first
            showStep(2);

            /*
             * Only show the location popup
             * after the user reaches Step 2.
             */
            if (
                !latitude?.value ||
                !longitude?.value
            ) {
                setTimeout(() => {
                    openLocationModal();
                }, 200);
            }
        }
    );

    nextStep2Button?.addEventListener(
        "click",
        () => {
            if (!validateStep2()) {
                return;
            }

            showStep(3);
        }
    );

    document.querySelectorAll("[data-back]")
        .forEach(button => {
            button.addEventListener(
                "click",
                () => {
                    closeLocationModal();

                    showStep(
                        Number(
                            button.dataset.back
                        )
                    );
                }
            );
        });

    // ==============================
    // Password show and hide
    // ==============================

    document.querySelectorAll(
        "[data-toggle-password]"
    ).forEach(button => {
        button.addEventListener(
            "click",
            () => {
                const input =
                    document.getElementById(
                        button.dataset
                            .togglePassword
                    );

                if (!input) {
                    return;
                }

                const showing =
                    input.type === "text";

                input.type =
                    showing
                        ? "password"
                        : "text";

                button.textContent =
                    showing
                        ? "Show"
                        : "Hide";

                button.setAttribute(
                    "aria-label",
                    showing
                        ? "Show password"
                        : "Hide password"
                );
            }
        );
    });

    // ==============================
    // Map setup
    // ==============================

    let map;
    let marker;

    if (
        window.L &&
        document.getElementById("map")
    ) {
        const savedLat =
            Number(latitude?.value);

        const savedLng =
            Number(longitude?.value);

        const hasSavedLocation =
            Number.isFinite(savedLat) &&
            Number.isFinite(savedLng) &&
            savedLat !== 0 &&
            savedLng !== 0;

        const start =
            hasSavedLocation
                ? [savedLat, savedLng]
                : [4.2105, 101.9758];

        map = L.map("map").setView(
            start,
            hasSavedLocation ? 15 : 6
        );

        L.tileLayer(
            "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
            {
                maxZoom: 19,
                attribution:
                    "&copy; OpenStreetMap contributors"
            }
        ).addTo(map);

        marker = L.marker(
            start,
            {
                draggable: true
            }
        ).addTo(map);

        if (!hasSavedLocation) {
            marker.setOpacity(0);
        }

        map.on(
            "click",
            event => {
                setLocation(
                    event.latlng.lat,
                    event.latlng.lng,
                    true
                );
            }
        );

        marker.on(
            "dragend",
            event => {
                const point =
                    event.target.getLatLng();

                setLocation(
                    point.lat,
                    point.lng,
                    true
                );
            }
        );
    }

    // ==============================
    // Reverse geocoding
    // ==============================

    async function reverseGeocode(
        lat,
        lng
    ) {
        try {
            const url =
                "https://nominatim.openstreetmap.org/reverse" +
                "?format=jsonv2" +
                `&lat=${encodeURIComponent(lat)}` +
                `&lon=${encodeURIComponent(lng)}`;

            const response =
                await fetch(
                    url,
                    {
                        headers: {
                            "Accept-Language": "en"
                        }
                    }
                );

            if (!response.ok) {
                throw new Error(
                    "Address lookup failed."
                );
            }

            const result =
                await response.json();

            return (
                result.display_name ||
                `${Number(lat).toFixed(6)}, ${Number(lng).toFixed(6)}`
            );

        } catch (error) {
            console.error(
                "Reverse geocoding error:",
                error
            );

            return (
                `${Number(lat).toFixed(6)}, ${Number(lng).toFixed(6)}`
            );
        }
    }

    // ==============================
    // Set selected location
    // ==============================

    async function setLocation(
        lat,
        lng,
        moveMap
    ) {
        if (
            !Number.isFinite(Number(lat)) ||
            !Number.isFinite(Number(lng))
        ) {
            if (locationError) {
                locationError.textContent =
                    "The selected location is invalid.";
            }

            return;
        }

        if (latitude) {
            latitude.value =
                Number(lat).toFixed(7);
        }

        if (longitude) {
            longitude.value =
                Number(lng).toFixed(7);
        }

        marker
            ?.setLatLng([lat, lng])
            .setOpacity(1);

        if (moveMap) {
            map?.setView(
                [lat, lng],
                16
            );
        }

        if (address) {
            address.value =
                "Finding address…";
        }

        const foundAddress =
            await reverseGeocode(
                lat,
                lng
            );

        if (address) {
            address.value =
                foundAddress;
        }

        if (locationError) {
            locationError.textContent = "";
        }
    }

    // ==============================
    // Browser location
    // ==============================

    function useCurrentLocation() {
        closeLocationModal();

        if (!navigator.geolocation) {
            if (locationError) {
                locationError.textContent =
                    "This browser does not support location access. Click the map instead.";
            }

            return;
        }

        if (locationError) {
            locationError.textContent =
                "Getting your location…";
        }

        navigator.geolocation
            .getCurrentPosition(
                position => {
                    setLocation(
                        position.coords.latitude,
                        position.coords.longitude,
                        true
                    );
                },

                error => {
                    console.error(
                        "Location error:",
                        error
                    );

                    if (!locationError) {
                        return;
                    }

                    if (
                        error.code ===
                        error.PERMISSION_DENIED
                    ) {
                        locationError.textContent =
                            "Location permission was denied. Click the map to choose manually.";

                    } else if (
                        error.code ===
                        error.POSITION_UNAVAILABLE
                    ) {
                        locationError.textContent =
                            "Your location is currently unavailable. Click the map to choose manually.";

                    } else if (
                        error.code ===
                        error.TIMEOUT
                    ) {
                        locationError.textContent =
                            "Location request timed out. Try again or click the map.";

                    } else {
                        locationError.textContent =
                            "Location access was unavailable. Click the map to choose manually.";
                    }
                },

                {
                    enableHighAccuracy: true,
                    timeout: 10000,
                    maximumAge: 60000
                }
            );
    }

    // Use Current Location button
    locateMeButton?.addEventListener(
        "click",
        () => {
            useCurrentLocation();
        }
    );

    // Allow Location button in modal
    allowLocationButton?.addEventListener(
        "click",
        () => {
            useCurrentLocation();
        }
    );

    // Skip Location button in modal
    skipLocationButton?.addEventListener(
        "click",
        () => {
            closeLocationModal();

            if (locationError) {
                locationError.textContent =
                    "Click the map to choose your location manually.";
            }

            setTimeout(() => {
                map?.invalidateSize();
            }, 100);
        }
    );

    /*
     * Clicking outside the modal does not close it.
     * The user must choose Allow or Skip.
     */

    // ==============================
    // Submit form
    // ==============================

    form.addEventListener(
        "submit",
        event => {
            const step1Valid =
                validateStep1();

            const step2Valid =
                validateStep2();

            const step3Valid =
                validateStep3();

            const allValid =
                step1Valid &&
                step2Valid &&
                step3Valid;

            if (!allValid) {
                event.preventDefault();

                if (!step1Valid) {
                    showStep(1);

                } else if (!step2Valid) {
                    showStep(2);

                } else {
                    showStep(3);
                }

                return;
            }

            closeLocationModal();

            if (submitButton) {
                submitButton.disabled = true;

                submitButton.textContent =
                    "Creating account…";
            }
        }
    );

    // ==============================
    // Initial page state
    // ==============================

    /*
     * The modal stays hidden when the page
     * initially loads.
     *
     * It only opens after the user completes
     * Step 1 and enters Step 2.
     */

    showStep(currentStep);

    updatePasswordRules();
})();