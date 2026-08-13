(() => {
    "use strict";

    console.log("Register JS loaded");

    // ==================================================
    // FORM
    // ==================================================

    const form =
        document.getElementById("multiStepForm");

    if (!form) {
        console.log(
            "Register JS: #multiStepForm not found."
        );

        return;
    }

    // ==================================================
    // FORM STEPS
    // ==================================================

    const steps = [
        ...document.querySelectorAll(".form-step")
    ];

    const nodes = [
        ...document.querySelectorAll(".step-node")
    ];

    const stepTitle =
        document.getElementById("stepTitle");

    const stepCount =
        document.getElementById("stepCount");

    const progressFill =
        document.getElementById("progressFill");

    const stepTitles = [
        "Profile details",
        "Location details",
        "Account security"
    ];

    let currentStep =
        Number(window.registerInitialStep || 1);

    // ==================================================
    // INPUTS
    // ==================================================

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
        document.getElementById(
            "clientLocationError"
        );

    const locationAccuracyInfo =
        document.getElementById(
            "locationAccuracyInfo"
        );

    const password =
        document.getElementById("passwordInput");

    const confirmPassword =
        document.getElementById(
            "confirmPasswordInput"
        );

    const confirmError =
        document.getElementById(
            "confirmClientError"
        );

    const submitButton =
        document.getElementById("submitButton");

    // ==================================================
    // BUTTONS
    // ==================================================

    const nextStep1Button =
        document.getElementById("nextStep1");

    const nextStep2Button =
        document.getElementById("nextStep2");

    const checkAvailabilityButton =
        document.getElementById(
            "checkAvailabilityButton"
        );

    const availabilityStatus =
        document.getElementById(
            "availabilityStatus"
        );

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

    // ==================================================
    // AJAX DUPLICATE CHECK
    // ==================================================

    let availabilityChecked = false;
    let checkedAvailabilityValues = null;
    let availabilityRequestNumber = 0;
    let availabilityHasDuplicate = false;

    function getAvailabilityValues() {
        return {
            username:
                username?.value.trim() || "",

            email:
                email?.value.trim()
                    .toLowerCase() || "",

            phone:
                (phone?.value || "")
                    .replace(/\D/g, "")
        };
    }

    function sameAvailabilityValues(
        first,
        second
    ) {
        return Boolean(
            first &&
            second &&
            first.username === second.username &&
            first.email === second.email &&
            first.phone === second.phone
        );
    }

    function setAvailabilityStatus(
        message,
        state = "idle"
    ) {
        if (!availabilityStatus) {
            return;
        }

        availabilityStatus.textContent =
            message || "";

        availabilityStatus.classList.remove(
            "is-idle",
            "is-checking",
            "is-success",
            "is-error"
        );

        availabilityStatus.classList.add(
            `is-${state}`
        );
    }

    function clearAvailabilityFieldState(
        input
    ) {
        const inputBox =
            input?.closest(".input-box");

        inputBox?.classList.remove(
            "ajax-valid",
            "ajax-invalid",
            "ajax-checking"
        );
    }

    function setAvailabilityFieldState(
        input,
        available,
        message
    ) {
        const inputBox =
            input?.closest(".input-box");

        if (!inputBox) {
            return;
        }

        inputBox.classList.remove(
            "ajax-valid",
            "ajax-invalid",
            "ajax-checking"
        );

        inputBox.classList.add(
            available
                ? "ajax-valid"
                : "ajax-invalid"
        );

        setCustomError(
            input,
            available
                ? ""
                : message
        );
    }

    // Check whether all three fields are complete
    // and have the correct format.
    function availabilityInputsAreComplete() {
        const values =
            getAvailabilityValues();

        const gmailPattern =
            /^[a-zA-Z0-9._%+-]+@gmail\.com$/i;

        const phonePattern =
            /^(011\d{8}|01[2-9]\d{7})$/;

        return (
            values.username.length >= 1 &&
            values.username.length <= 30 &&
            gmailPattern.test(values.email) &&
            phonePattern.test(values.phone)
        );
    }

    // Control the Check availability button.
    function updateAvailabilityButton() {
        if (!checkAvailabilityButton) {
            return;
        }

        const complete =
            availabilityInputsAreComplete();

        const currentValues =
            getAvailabilityValues();

        const alreadyChecked =
            availabilityChecked &&
            sameAvailabilityValues(
                currentValues,
                checkedAvailabilityValues
            );

        checkAvailabilityButton.classList.remove(
            "availability-incomplete",
            "availability-ready",
            "availability-success",
            "availability-duplicate"
        );

        if (!complete) {
            checkAvailabilityButton.disabled = true;

            checkAvailabilityButton.textContent =
                "Complete all fields first";

            checkAvailabilityButton.classList.add(
                "availability-incomplete"
            );

            return;
        }

        if (alreadyChecked) {
            checkAvailabilityButton.disabled = true;

            checkAvailabilityButton.textContent =
                "Availability checked";

            checkAvailabilityButton.classList.add(
                "availability-success"
            );

            return;
        }

        checkAvailabilityButton.disabled = false;

        checkAvailabilityButton.textContent =
            "Check availability";

        checkAvailabilityButton.classList.add(
            "availability-ready"
        );
        if (availabilityHasDuplicate) {
            checkAvailabilityButton.disabled = true;

            checkAvailabilityButton.textContent =
                "Change duplicate details";

            checkAvailabilityButton.classList.add(
                "availability-duplicate"
            );

            return;
        }
    }
    function invalidateAvailabilityCheck() {
        availabilityChecked = false;
        checkedAvailabilityValues = null;

        [username, email, phone]
            .forEach(input => {
                clearAvailabilityFieldState(
                    input
                );
            });

        if (availabilityInputsAreComplete()) {
            setAvailabilityStatus(
                "Press Check availability to continue.",
                "idle"
            );
        }
        else {
            setAvailabilityStatus(
                "Complete the username, Gmail address and phone number first.",
                "idle"
            );
        }

        updateAvailabilityButton();
    }

    function validateAvailabilityInputs() {
        let valid = true;

        const gmailPattern =
            /^[a-zA-Z0-9._%+-]+@gmail\.com$/i;

        const phonePattern =
            /^(011\d{8}|01[2-9]\d{7})$/;

        const usernameValue =
            username?.value.trim() || "";

        const emailValue =
            email?.value.trim() || "";

        const phoneValue =
            (phone?.value || "")
                .replace(/\D/g, "");

        setCustomError(username, "");
        setCustomError(email, "");
        setCustomError(phone, "");

        if (!usernameValue) {
            setCustomError(
                username,
                "Username is required."
            );

            valid = false;
        }
        else if (
            usernameValue.length > 30
        ) {
            setCustomError(
                username,
                "Username cannot exceed 30 characters."
            );

            valid = false;
        }

        if (!emailValue) {
            setCustomError(
                email,
                "Gmail address is required."
            );

            valid = false;
        }
        else if (
            !gmailPattern.test(emailValue)
        ) {
            setCustomError(
                email,
                "Use a valid Gmail address ending with @gmail.com."
            );

            valid = false;
        }

        if (!phoneValue) {
            setCustomError(
                phone,
                "Phone number is required."
            );

            valid = false;
        }
        else if (
            !phonePattern.test(phoneValue)
        ) {
            setCustomError(
                phone,
                "Enter a valid Malaysian phone number."
            );

            valid = false;
        }

        return valid;
    }

    async function checkRegistrationAvailability() {
        if (!validateAvailabilityInputs()) {
            availabilityChecked = false;
            checkedAvailabilityValues = null;

            setAvailabilityStatus(
                "Complete all fields correctly before checking.",
                "error"
            );

            updateAvailabilityButton();

            form.querySelector(":invalid")
                ?.reportValidity();

            return;
        }

        const requestValues =
            getAvailabilityValues();

        const requestNumber =
            ++availabilityRequestNumber;

        [username, email, phone]
            .forEach(input => {
                clearAvailabilityFieldState(
                    input
                );

                input
                    ?.closest(".input-box")
                    ?.classList.add(
                        "ajax-checking"
                    );
            });

        if (checkAvailabilityButton) {
            checkAvailabilityButton.disabled =
                true;

            checkAvailabilityButton.textContent =
                "Checking…";
        }

        setAvailabilityStatus(
            "Checking the database…",
            "checking"
        );

        const token =
            form.querySelector(
                'input[name="__RequestVerificationToken"]'
            );

        const body =
            new FormData();

        body.append(
            "username",
            requestValues.username
        );

        body.append(
            "email",
            requestValues.email
        );

        body.append(
            "phone",
            requestValues.phone
        );

        if (token?.value) {
            body.append(
                "__RequestVerificationToken",
                token.value
            );
        }

        try {
            const response =
                await fetch(
                    window.registerAvailabilityUrl ||
                    "/Account/CheckRegistrationAvailability",
                    {
                        method: "POST",

                        body: body,

                        headers: {
                            "X-Requested-With":
                                "XMLHttpRequest"
                        }
                    }
                );

            let result;

            try {
                result =
                    await response.json();
            }
            catch {
                throw new Error(
                    "The server returned an invalid response."
                );
            }

            if (
                requestNumber !==
                availabilityRequestNumber
            ) {
                return;
            }

            if (
                !response.ok ||
                !result.success
            ) {
                const fieldInput =
                    result.field === "username"
                        ? username
                        : result.field === "email"
                            ? email
                            : result.field === "phone"
                                ? phone
                                : null;

                if (fieldInput) {
                    setAvailabilityFieldState(
                        fieldInput,
                        false,
                        result.message
                    );
                }

                availabilityChecked = false;
                checkedAvailabilityValues = null;

                setAvailabilityStatus(
                    result.message ||
                    "Unable to check the details.",
                    "error"
                );

                return;
            }

            setAvailabilityFieldState(
                username,
                result.username.available,
                result.username.message
            );

            setAvailabilityFieldState(
                email,
                result.email.available,
                result.email.message
            );

            setAvailabilityFieldState(
                phone,
                result.phone.available,
                result.phone.message
            );

            availabilityChecked =
                Boolean(
                    result.allAvailable
                );

            checkedAvailabilityValues =
                availabilityChecked
                    ? requestValues
                    : null;

            if (availabilityChecked) {
                setAvailabilityStatus(
                    "All details are available. You may continue.",
                    "success"
                );
            }
            else {
                const duplicateMessages = [
                    result.username,
                    result.email,
                    result.phone
                ]
                    .filter(item => !item.available)
                    .map(item => item.message);

                setAvailabilityStatus(
                    duplicateMessages.join(" "),
                    "error"
                );

                if (checkAvailabilityButton) {
                    checkAvailabilityButton.disabled = true;

                    checkAvailabilityButton.textContent =
                        "Change duplicate details";

                    checkAvailabilityButton.classList.remove(
                        "availability-incomplete",
                        "availability-ready",
                        "availability-success"
                    );

                    checkAvailabilityButton.classList.add(
                        "availability-duplicate"
                    );
                }
            }
        }
        catch (error) {
            console.error(
                "Availability check error:",
                error
            );

            availabilityChecked = false;
            checkedAvailabilityValues = null;

            [username, email, phone]
                .forEach(input => {
                    clearAvailabilityFieldState(
                        input
                    );
                });

            setAvailabilityStatus(
                "Unable to check the database. Check your connection and try again.",
                "error"
            );
        }
        finally {
            if (
                requestNumber ===
                availabilityRequestNumber
            ) {
                updateAvailabilityButton();
            }
        }
    }

    // ==================================================
    // LOCATION MODAL
    // ==================================================

    const locationModal =
        document.getElementById(
            "locationModal"
        );

    function openLocationModal() {
        if (locationModal) {
            locationModal.hidden = false;
        }
    }

    function closeLocationModal() {
        if (locationModal) {
            locationModal.hidden = true;
        }
    }

    closeLocationModal();

    // ==================================================
    // GENERAL ERROR HELPERS
    // ==================================================

    function setCustomError(
        input,
        message
    ) {
        if (!input) {
            return;
        }

        input.setCustomValidity(
            message || ""
        );

        const field =
            input.closest(
                ".field, fieldset"
            );

        const errorElement =
            field?.querySelector(
                ".field-error"
            );

        if (
            errorElement &&
            !errorElement.dataset.serverError
        ) {
            errorElement.textContent =
                message || "";
        }
    }

    function setLocationMessage(
        message,
        isError = false
    ) {
        if (!locationError) {
            return;
        }

        locationError.textContent =
            message || "";

        locationError.classList.toggle(
            "location-danger",
            Boolean(message && isError)
        );

        locationError.classList.toggle(
            "location-warning",
            Boolean(message && !isError)
        );
    }

    function clearLocationMessage() {
        setLocationMessage("");
    }

    // ==================================================
    // SHOW STEP
    // ==================================================

    function showStep(number) {
        currentStep =
            Math.max(
                1,
                Math.min(3, number)
            );

        steps.forEach(step => {
            const numberOfStep =
                Number(step.dataset.step);

            step.classList.toggle(
                "active",
                numberOfStep === currentStep
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

        if (stepTitle) {
            stepTitle.textContent =
                stepTitles[currentStep - 1];
        }

        if (stepCount) {
            stepCount.textContent =
                `Step ${currentStep} of 3`;
        }

        if (progressFill) {
            progressFill.style.width =
                `${((currentStep - 1) / 2) *
                100
                }%`;
        }

        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });

        if (currentStep === 2) {
            setTimeout(() => {
                map?.invalidateSize();
            }, 150);
        }
    }

    // ==================================================
    // STEP 1 VALIDATION
    // ==================================================

    function validateStep1() {
        let valid = true;

        const gmailPattern =
            /^[a-zA-Z0-9._%+-]+@gmail\.com$/i;

        const phonePattern =
            /^(011\d{8}|01[2-9]\d{7})$/;

        setCustomError(username, "");
        setCustomError(email, "");
        setCustomError(phone, "");

        if (!username?.value.trim()) {
            setCustomError(
                username,
                "Username is required."
            );

            valid = false;
        }
        else if (
            username.value.trim().length > 30
        ) {
            setCustomError(
                username,
                "Username cannot exceed 30 characters."
            );

            valid = false;
        }

        if (!email?.value.trim()) {
            setCustomError(
                email,
                "Email is required."
            );

            valid = false;
        }
        else if (
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

        if (!phone?.value.trim()) {
            setCustomError(
                phone,
                "Phone number is required."
            );

            valid = false;
        }
        else if (
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
        }
        else if (genderError) {
            genderError.textContent = "";
        }

        if (!valid) {
            form.querySelector(":invalid")
                ?.reportValidity();
        }

        return valid;
    }

    // ==================================================
    // STEP 2 VALIDATION
    // ==================================================

    function validateStep2() {
        const addressValue =
            address?.value.trim() || "";

        const latitudeValue =
            latitude?.value.trim() || "";

        const longitudeValue =
            longitude?.value.trim() || "";

        const hasCompleteLocation =
            Boolean(addressValue) &&
            Boolean(latitudeValue) &&
            Boolean(longitudeValue);

        const valid =
            hasCompleteLocation &&
            locationSelectionIsValid;

        if (!valid) {
            setLocationMessage(
                "Please select a valid residential address, road or building before continuing.",
                true
            );

            return false;
        }

        clearLocationMessage();

        return true;
    }

    // ==================================================
    // PASSWORD RULES
    // ==================================================

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

        Object.entries(
            passwordRules
        ).forEach(([name, test]) => {
            const ruleElement =
                document.querySelector(
                    `[data-rule="${name}"]`
                );

            ruleElement?.classList.toggle(
                "valid",
                test(value)
            );
        });

        const allValid =
            Object.values(
                passwordRules
            ).every(test => test(value));

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

    // ==================================================
    // STEP 3 VALIDATION
    // ==================================================

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
        }
        else if (!updatePasswordRules()) {
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
        }
        else if (
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

    // ==================================================
    // INPUT EVENTS
    // ==================================================

    phone?.addEventListener(
        "input",
        () => {
            phone.value =
                phone.value
                    .replace(/\D/g, "")
                    .slice(0, 11);

            setCustomError(phone, "");
            invalidateAvailabilityCheck();
        }
    );

    username?.addEventListener(
        "input",
        () => {
            setCustomError(username, "");
            invalidateAvailabilityCheck();
        }
    );

    email?.addEventListener(
        "input",
        () => {
            setCustomError(email, "");
            invalidateAvailabilityCheck();
        }
    );

    checkAvailabilityButton
        ?.addEventListener(
            "click",
            checkRegistrationAvailability
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
            }
            else if (confirmError) {
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

    // ==================================================
    // STEP NAVIGATION
    // ==================================================

    nextStep1Button?.addEventListener(
        "click",
        () => {
            if (!validateStep1()) {
                return;
            }

            const currentValues =
                getAvailabilityValues();

            if (
                !availabilityChecked ||
                !sameAvailabilityValues(
                    currentValues,
                    checkedAvailabilityValues
                )
            ) {
                setAvailabilityStatus(
                    "Please press Check availability and pass the duplicate check before continuing.",
                    "error"
                );

                checkAvailabilityButton?.focus();
                return;
            }

            showStep(2);

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

    document.querySelectorAll(
        "[data-back]"
    ).forEach(button => {
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

    // ==================================================
    // PASSWORD SHOW / HIDE
    // ==================================================

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

                const currentlyShowing =
                    input.type === "text";

                input.type =
                    currentlyShowing
                        ? "password"
                        : "text";

                button.textContent =
                    currentlyShowing
                        ? "Show"
                        : "Hide";

                button.setAttribute(
                    "aria-label",
                    currentlyShowing
                        ? "Show password"
                        : "Hide password"
                );
            }
        );
    });

    // ==================================================
    // MAP CONFIGURATION
    // ==================================================

    const LOCATION_CONFIG = {
        defaultLatitude: 5.4164,
        defaultLongitude: 100.3327,
        defaultZoom: 13,
        selectedZoom: 17,

        excellentAccuracy: 25,
        acceptableAccuracy: 80,
        maximumAcceptedAccuracy: 250,

        minimumSamples: 2,
        maximumSamples: 8,
        watchTimeoutMs: 10000
    };

    let map = null;
    let marker = null;
    let accuracyCircle = null;
    let locationWatchId = null;

    // Location mode prevents old Geolocation callbacks from
    // overriding a point that the user selected manually.
    let locationMode = "idle";
    let locationSessionNumber = 0;

    // True only when the currently selected location has passed
    // all residential/building validation rules.
    let locationSelectionIsValid = false;

    /*
     * Used to prevent an older reverse-geocoding
     * request from overwriting a newer marker position.
     */
    let reverseGeocodeRequestNumber = 0;

    // ==================================================
    // MAP INITIALISATION
    // ==================================================

    function initialiseMap() {
        const mapElement =
            document.getElementById("map");

        if (!mapElement) {
            return;
        }

        if (!window.L) {
            setLocationMessage(
                "The map library failed to load. Please refresh the page.",
                true
            );

            return;
        }

        const savedLatitude =
            Number(latitude?.value);

        const savedLongitude =
            Number(longitude?.value);

        const hasSavedLocation =
            Number.isFinite(savedLatitude) &&
            Number.isFinite(savedLongitude) &&
            savedLatitude !== 0 &&
            savedLongitude !== 0;

        const startingPoint =
            hasSavedLocation
                ? [
                    savedLatitude,
                    savedLongitude
                ]
                : [
                    LOCATION_CONFIG
                        .defaultLatitude,

                    LOCATION_CONFIG
                        .defaultLongitude
                ];

        map =
            L.map("map").setView(
                startingPoint,
                hasSavedLocation
                    ? LOCATION_CONFIG.selectedZoom
                    : LOCATION_CONFIG.defaultZoom
            );

        L.tileLayer(
            "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
            {
                maxZoom: 19,

                attribution:
                    "&copy; OpenStreetMap contributors"
            }
        ).addTo(map);

        marker =
            L.marker(
                startingPoint,
                {
                    draggable: true
                }
            ).addTo(map);

        if (!hasSavedLocation) {
            marker.setOpacity(0);
        }

        map.on(
            "click",
            async event => {
                cancelAutomaticLocation();

                await setLocation(
                    event.latlng.lat,
                    event.latlng.lng,
                    true,
                    null,
                    true
                );
            }
        );

        marker.on(
            "dragend",
            async event => {
                cancelAutomaticLocation();

                const point =
                    event.target.getLatLng();

                await setLocation(
                    point.lat,
                    point.lng,
                    true,
                    null,
                    true
                );
            }
        );
    }

    initialiseMap();

    // ==================================================
    // MAP HELPERS
    // ==================================================

    function clearAccuracyCircle() {
        if (
            accuracyCircle &&
            map
        ) {
            map.removeLayer(
                accuracyCircle
            );
        }

        accuracyCircle = null;
    }

    function showAccuracyCircle(
        lat,
        lng,
        accuracy
    ) {
        if (
            !map ||
            !Number.isFinite(
                Number(accuracy)
            )
        ) {
            return;
        }

        clearAccuracyCircle();

        accuracyCircle =
            L.circle(
                [lat, lng],
                {
                    radius:
                        Math.max(
                            Number(accuracy),
                            5
                        ),

                    weight: 1,
                    fillOpacity: 0.08
                }
            ).addTo(map);
    }

    function setAccuracyMessage(
        accuracy
    ) {
        if (!locationAccuracyInfo) {
            return;
        }

        if (
            !Number.isFinite(
                Number(accuracy)
            )
        ) {
            locationAccuracyInfo.textContent =
                "";

            return;
        }

        const roundedAccuracy =
            Math.round(
                Number(accuracy)
            );

        let quality;

        if (
            roundedAccuracy <=
            LOCATION_CONFIG.excellentAccuracy
        ) {
            quality = "Excellent";
        }
        else if (
            roundedAccuracy <=
            LOCATION_CONFIG.acceptableAccuracy
        ) {
            quality = "Good";
        }
        else if (
            roundedAccuracy <=
            LOCATION_CONFIG.maximumAcceptedAccuracy
        ) {
            quality = "Fair";
        }
        else {
            quality = "Poor";
        }

        locationAccuracyInfo.textContent =
            `${quality} accuracy: approximately ${roundedAccuracy} metres.`;
    }

    function clearStoredLocation(
        hideMarker = true
    ) {
        locationSelectionIsValid = false;

        if (latitude) {
            latitude.value = "";
        }

        if (longitude) {
            longitude.value = "";
        }

        if (address) {
            address.value = "";
        }

        if (hideMarker) {
            marker?.setOpacity(0);
        }

        clearAccuracyCircle();
    }
    // ==================================================
    // CALCULATE DISTANCE BETWEEN TWO COORDINATES
    // ==================================================

    function calculateDistanceMetres(
        lat1,
        lng1,
        lat2,
        lng2
    ) {
        const earthRadiusMetres = 6371000;

        const toRadians =
            degrees =>
                degrees * Math.PI / 180;

        const firstLatitude =
            toRadians(lat1);

        const secondLatitude =
            toRadians(lat2);

        const latitudeDifference =
            toRadians(lat2 - lat1);

        const longitudeDifference =
            toRadians(lng2 - lng1);

        const value =
            Math.sin(
                latitudeDifference / 2
            ) ** 2 +
            Math.cos(firstLatitude) *
            Math.cos(secondLatitude) *
            Math.sin(
                longitudeDifference / 2
            ) ** 2;

        const angularDistance =
            2 * Math.atan2(
                Math.sqrt(value),
                Math.sqrt(1 - value)
            );

        return (
            earthRadiusMetres *
            angularDistance
        );
    }
    // ==================================================
    // LOCATION VALIDATION
    // ==================================================

    function analyseLocationData(
        data,
        selectedLat,
        selectedLng
    ) {
        if (
            !data ||
            data.error ||
            !data.address
        ) {
            return {
                valid: false,
                reason:
                    "No valid address was found."
            };
        }

        const displayName =
            (
                data.display_name || ""
            ).toLowerCase();

        const locationType =
            (
                data.type || ""
            ).toLowerCase();

        const locationClass =
            (
                data.class || ""
            ).toLowerCase();

        const addressType =
            (
                data.addresstype || ""
            ).toLowerCase();

        const locationName =
            (
                data.name || ""
            ).toLowerCase();

        const addressDetails =
            data.address || {};

        const combinedText =
            `
            ${displayName}
            ${locationName}
            ${locationType}
            ${locationClass}
            ${addressType}
        `.toLowerCase();

        // ==================================================
        // 1. BLOCK WATER / FOREST / INVALID AREAS
        // ==================================================

        const blockedKeywords = [
            "ocean",
            "sea",
            "river",
            "lake",
            "strait",
            "channel",
            "reservoir",
            "waterfall",
            "waterway",
            "forest",
            "wood",
            "woods",
            "jungle",
            "desert",
            "wetland",
            "swamp",
            "marsh",
            "bridge",
            "islet",
            "sandbank",
            "bay",
            "coastline",
            "mountain",
            "hill",
            "peak",
            "nature reserve",
            "national park",
            "protected area"
        ];

        const blockedTypes = [
            "water",
            "river",
            "sea",
            "lake",
            "reservoir",
            "strait",
            "forest",
            "wood",
            "desert",
            "wetland",
            "bay",
            "peak",
            "cliff",
            "scrub",
            "heath",
            "grassland"
        ];

        const keywordBlocked =
            blockedKeywords.some(
                word =>
                    combinedText.includes(word)
            );

        const typeBlocked =
            blockedTypes.includes(
                locationType
            ) ||
            blockedTypes.includes(
                addressType
            );

        const naturalWithoutAddress =
            locationClass === "natural" &&
            !addressDetails.road &&
            !addressDetails.house_number &&
            !addressDetails.building &&
            !addressDetails.residential &&
            !addressDetails.neighbourhood &&
            !addressDetails.suburb;

        if (
            keywordBlocked ||
            typeBlocked ||
            naturalWithoutAddress
        ) {
            return {
                valid: false,

                reason:
                    "Sea, forest, mountain, river, lake and other non-residential locations are not allowed."
            };
        }

        const naturalOrLeisureArea =
            ["natural", "leisure"].includes(
                locationClass
            ) &&
            !addressDetails.house_number &&
            !addressDetails.building &&
            !addressDetails.road &&
            !addressDetails.residential;

        if (naturalOrLeisureArea) {
            return {
                valid: false,

                reason:
                    "This natural or recreational area is not a valid residential address. Please select a road or building."
            };
        }

        // ==================================================
        // 2. CHECK DISTANCE BETWEEN SELECTED POINT
        //    AND NOMINATIM RESULT
        // ==================================================

        const resultLatitude =
            Number(data.lat);

        const resultLongitude =
            Number(data.lon);

        if (
            Number.isFinite(resultLatitude) &&
            Number.isFinite(resultLongitude) &&
            Number.isFinite(
                Number(selectedLat)
            ) &&
            Number.isFinite(
                Number(selectedLng)
            )
        ) {
            const distanceMetres =
                calculateDistanceMetres(
                    Number(selectedLat),
                    Number(selectedLng),
                    resultLatitude,
                    resultLongitude
                );

            console.log(
                "Selected coordinate:",
                selectedLat,
                selectedLng
            );

            console.log(
                "Nominatim result coordinate:",
                resultLatitude,
                resultLongitude
            );

            console.log(
                "Distance from selected point to returned address:",
                Math.round(distanceMetres),
                "metres"
            );

            /*
             * Distance between the exact selected point and the
             * coordinate returned by Nominatim.
             *
             * For residential/building selection:
             * - <= 100m: acceptable
             * - 101m-250m: still usable, but approximate
             * - > 250m: reject because the returned address is too far
             *
             * Do not show huge distance values to the user. They are
             * technically correct but confusing when the point is in
             * the sea, forest, empty land or another unmapped area.
             */
            if (distanceMetres > 250) {
                return {
                    valid: false,

                    reason:
                        "This point is too far from a recognised road or building. Please place the marker on a valid residential or building location."
                };
            }

            if (distanceMetres > 100) {
                return {
                    valid: false,
                    reason:
                        "The selected point is too far from an exact road or building. Please move the marker to your actual residential address."
                };
            }
        }

        // ==================================================
        // 3. BLOCK OVERLY BROAD LOCATIONS
        // ==================================================

        const broadAddressTypes = [
            "country",
            "state",
            "region",
            "province",
            "county"
        ];

        const tooBroadType =
            broadAddressTypes.includes(
                addressType
            );

        const onlyCountryOrState =
            Boolean(
                addressDetails.country
            ) &&
            !addressDetails.road &&
            !addressDetails.house_number &&
            !addressDetails.building &&
            !addressDetails.neighbourhood &&
            !addressDetails.suburb &&
            !addressDetails.residential &&
            !addressDetails.village &&
            !addressDetails.town &&
            !addressDetails.city &&
            !addressDetails.city_district;

        if (
            tooBroadType ||
            onlyCountryOrState
        ) {
            return {
                valid: false,

                reason:
                    "The selected location is too broad. Zoom in and select a road, building or residential area."
            };
        }

        // ==================================================
        // 4. REQUIRE LOCAL ADDRESS INFORMATION
        // ==================================================

        const hasExactAddressContext =
            addressDetails.house_number ||
            addressDetails.building ||
            addressDetails.amenity ||
            addressDetails.shop ||
            addressDetails.office ||
            addressDetails.road ||
            addressDetails.pedestrian ||
            addressDetails.residential;

        if (!hasExactAddressContext) {
            return {
                valid: false,

                reason:
                    "This location does not contain a recognised residential address, road or building. Please select your actual address."
            };
        }

        return {
            valid: true,
            reason: "OK"
        };
    }

    // ==================================================
    // REVERSE GEOCODING
    // ==================================================

    async function reverseGeocode(
        lat,
        lng
    ) {
        const currentRequest =
            ++reverseGeocodeRequestNumber;

        const url =
            "https://nominatim.openstreetmap.org/reverse" +
            "?format=jsonv2" +
            `&lat=${encodeURIComponent(lat)}` +
            `&lon=${encodeURIComponent(lng)}` +
            "&zoom=18" +
            "&addressdetails=1";

        const response =
            await fetch(
                url,
                {
                    headers: {
                        "Accept-Language":
                            "en"
                    }
                }
            );

        if (!response.ok) {
            throw new Error(
                `Address lookup failed with HTTP ${response.status}.`
            );
        }

        const result =
            await response.json();

        /*
         * Ignore this result if the user already
         * selected another newer location.
         */
        if (
            currentRequest !==
            reverseGeocodeRequestNumber
        ) {
            return {
                cancelled: true
            };
        }

        return {
            cancelled: false,
            data: result
        };
    }

    // ==================================================
    // SET LOCATION
    // ==================================================

    async function setLocation(
        lat,
        lng,
        moveMap = true,
        accuracy = null,
        manualSelection = false
    ) {
        locationSelectionIsValid = false;

        const numericLat =
            Number(lat);

        const numericLng =
            Number(lng);

        if (
            !Number.isFinite(numericLat) ||
            !Number.isFinite(numericLng) ||
            numericLat < -90 ||
            numericLat > 90 ||
            numericLng < -180 ||
            numericLng > 180
        ) {
            setLocationMessage(
                "The selected coordinates are invalid.",
                true
            );

            return false;
        }

        marker
            ?.setLatLng([
                numericLat,
                numericLng
            ])
            .setOpacity(1);

        if (moveMap) {
            map?.setView(
                [
                    numericLat,
                    numericLng
                ],
                LOCATION_CONFIG.selectedZoom
            );
        }

        if (
            accuracy !== null &&
            Number.isFinite(
                Number(accuracy)
            )
        ) {
            showAccuracyCircle(
                numericLat,
                numericLng,
                accuracy
            );

            setAccuracyMessage(
                accuracy
            );
        }
        else {
            clearAccuracyCircle();

            if (locationAccuracyInfo) {
                locationAccuracyInfo.textContent =
                    manualSelection
                        ? "Manual location selected — place the marker on your exact building or road."
                        : "";
            }
        }

        if (address) {
            address.value =
                "Finding address…";
        }

        setLocationMessage(
            "Checking the selected address…"
        );

        try {
            const result =
                await reverseGeocode(
                    numericLat,
                    numericLng
                );

            if (result.cancelled) {
                return false;
            }

            const analysis =
                analyseLocationData(
                    result.data,
                    numericLat,
                    numericLng
                );

            if (!analysis.valid) {
                clearStoredLocation();

                setLocationMessage(
                    analysis.reason,
                    true
                );

                return false;
            }

            if (latitude) {
                latitude.value =
                    numericLat.toFixed(7);
            }

            if (longitude) {
                longitude.value =
                    numericLng.toFixed(7);
            }

            if (address) {
                address.value =
                    result.data.display_name ||
                    `${numericLat.toFixed(7)}, ${numericLng.toFixed(7)}`;
            }

            locationSelectionIsValid = true;

            if (
                accuracy !== null &&
                Number(accuracy) >
                LOCATION_CONFIG
                    .maximumAcceptedAccuracy
            ) {
                locationSelectionIsValid = false;

                setLocationMessage(
                    `The detected location may be inaccurate by about ${Math.round(Number(accuracy))} metres. Please drag the marker to your exact building or use your location again.`,
                    true
                );

                return false;
            }

            if (
                accuracy !== null &&
                Number(accuracy) >
                LOCATION_CONFIG
                    .acceptableAccuracy
            ) {
                locationSelectionIsValid = false;

                setLocationMessage(
                    "The detected position is still too approximate. Please wait for a better reading, drag the marker to your exact building, or use your location again.",
                    true
                );

                return false;
            }

            clearLocationMessage();

            return true;
        }
        catch (error) {
            console.error(
                "Reverse geocoding error:",
                error
            );

            clearStoredLocation();

            setLocationMessage(
                "The address could not be verified. Check your internet connection and try again.",
                true
            );

            return false;
        }
    }

    // ==================================================
    // LOCATION BUTTON STATE
    // ==================================================

    function setLocateButtonBusy(
        busy
    ) {
        if (!locateMeButton) {
            return;
        }

        if (busy) {
            locateMeButton.dataset
                .originalLabel =
                locateMeButton.dataset
                    .originalLabel ||
                locateMeButton.textContent;

            // Keep the button clickable.
            // Pressing it again starts a fresh Geolocation session.
            locateMeButton.disabled = false;

            locateMeButton.textContent =
                "↻ Locating…";
        }
        else {
            locateMeButton.disabled = false;

            locateMeButton.textContent =
                locateMeButton.dataset
                    .originalLabel ||
                "◎ Use my location";
        }
    }

    // ==================================================
    // GEOLOCATION WATCH
    // ==================================================

    function stopLocationWatch() {
        if (
            locationWatchId !== null &&
            navigator.geolocation
        ) {
            navigator.geolocation
                .clearWatch(
                    locationWatchId
                );

            locationWatchId = null;
        }
    }

    function cancelAutomaticLocation() {
        locationMode = "manual";
        locationSessionNumber++;

        stopLocationWatch();
        setLocateButtonBusy(false);

        clearAccuracyCircle();
        setAccuracyMessage(NaN);
    }

    function useCurrentLocation() {
        closeLocationModal();

        if (!navigator.geolocation) {
            setLocationMessage(
                "This browser does not support location access. Select your location manually on the map.",
                true
            );

            return;
        }

        // Always start a completely fresh automatic-location session.
        // This lets the user return to the real current position no
        // matter how many manual points were selected before.
        stopLocationWatch();
        setLocateButtonBusy(false);

        locationSelectionIsValid = false;
        locationMode = "automatic";

        const currentSession =
            ++locationSessionNumber;

        setLocateButtonBusy(true);

        setLocationMessage(
            "Collecting several readings to find the most accurate location…"
        );

        setAccuracyMessage(NaN);

        // Do not show a stale accuracy circle from a previous result
        // while a new location is being calculated.
        clearAccuracyCircle();

        const samples = [];

        const startedAt =
            Date.now();

        let finished = false;
        let initialNavigationDone = false;

        function sessionIsStillActive() {
            return (
                locationMode === "automatic" &&
                currentSession ===
                locationSessionNumber
            );
        }

        async function finishWithBestSample() {
            if (
                finished ||
                !sessionIsStillActive()
            ) {
                return;
            }

            finished = true;

            stopLocationWatch();

            const validSamples =
                samples
                    .filter(sample =>
                        Number.isFinite(
                            sample.latitude
                        ) &&
                        Number.isFinite(
                            sample.longitude
                        ) &&
                        Number.isFinite(
                            sample.accuracy
                        ) &&
                        sample.accuracy > 0
                    )
                    .sort(
                        (first, second) =>
                            first.accuracy -
                            second.accuracy
                    );

            const bestSample =
                validSamples[0];

            if (!bestSample) {
                if (
                    sessionIsStillActive()
                ) {
                    locationMode = "idle";

                    setLocationMessage(
                        "No location reading was received. Select your location manually on the map.",
                        true
                    );

                    setLocateButtonBusy(false);
                }

                return;
            }

            // Re-check immediately before changing the map.
            // If the user clicked or dragged the map while location
            // was being calculated, the automatic result is ignored.
            if (!sessionIsStillActive()) {
                return;
            }

            const locationSet =
                await setLocation(
                    bestSample.latitude,
                    bestSample.longitude,
                    true,
                    bestSample.accuracy,
                    false
                );

            if (!sessionIsStillActive()) {
                setLocateButtonBusy(false);
                return;
            }

            locationMode = "idle";
            setLocateButtonBusy(false);
        }

        locationWatchId =
            navigator.geolocation
                .watchPosition(
                    position => {
                        if (
                            finished ||
                            !sessionIsStillActive()
                        ) {
                            return;
                        }

                        const sample = {
                            latitude:
                                position.coords
                                    .latitude,

                            longitude:
                                position.coords
                                    .longitude,

                            accuracy:
                                Number(
                                    position.coords
                                        .accuracy
                                ),

                            timestamp:
                                position.timestamp
                        };

                        if (
                            !Number.isFinite(
                                sample.latitude
                            ) ||
                            !Number.isFinite(
                                sample.longitude
                            ) ||
                            !Number.isFinite(
                                sample.accuracy
                            ) ||
                            sample.accuracy <= 0
                        ) {
                            return;
                        }

                        /*
                         * First valid reading:
                         * immediately take the user to the detected area.
                         *
                         * IMPORTANT:
                         * This happens only once per Use my location session.
                         * Later readings improve accuracy in the background
                         * without repeatedly pulling the map back.
                         */
                        if (!initialNavigationDone) {
                            initialNavigationDone = true;

                            marker
                                ?.setLatLng([
                                    sample.latitude,
                                    sample.longitude
                                ])
                                .setOpacity(1);

                            map?.setView(
                                [
                                    sample.latitude,
                                    sample.longitude
                                ],
                                LOCATION_CONFIG
                                    .selectedZoom
                            );

                            showAccuracyCircle(
                                sample.latitude,
                                sample.longitude,
                                sample.accuracy
                            );
                        }
                        else {
                            /*
                             * Real-time position refinement:
                             * update the marker and accuracy circle,
                             * but DO NOT recenter the map again.
                             *
                             * This keeps the location live without
                             * making the map jump back repeatedly.
                             */
                            marker
                                ?.setLatLng([
                                    sample.latitude,
                                    sample.longitude
                                ])
                                .setOpacity(1);

                            showAccuracyCircle(
                                sample.latitude,
                                sample.longitude,
                                sample.accuracy
                            );
                        }

                        samples.push(sample);

                        /*
                         * Keep only the most recent maximum
                         * number of samples.
                         */
                        if (
                            samples.length >
                            LOCATION_CONFIG
                                .maximumSamples
                        ) {
                            samples.shift();
                        }

                        const validAccuracyValues =
                            samples
                                .map(
                                    item =>
                                        item.accuracy
                                )
                                .filter(
                                    value =>
                                        Number.isFinite(
                                            value
                                        ) &&
                                        value > 0
                                );

                        const bestAccuracy =
                            validAccuracyValues.length
                                ? Math.min(
                                    ...validAccuracyValues
                                )
                                : NaN;

                        /*
                         * Keep the accuracy text live while better samples
                         * are collected. The marker and accuracy circle may
                         * update, but the map view itself is not recentered
                         * after the first valid reading.
                         */
                        setAccuracyMessage(
                            bestAccuracy
                        );

                        const enoughSamples =
                            samples.length >=
                            LOCATION_CONFIG
                                .minimumSamples;

                        const excellentReading =
                            sample.accuracy <=
                            LOCATION_CONFIG
                                .excellentAccuracy;

                        const acceptableBestReading =
                            enoughSamples &&
                            bestAccuracy <=
                            LOCATION_CONFIG
                                .acceptableAccuracy;

                        const timedOut =
                            Date.now() -
                            startedAt >=
                            LOCATION_CONFIG
                                .watchTimeoutMs;

                        if (
                            excellentReading ||
                            acceptableBestReading ||
                            timedOut
                        ) {
                            finishWithBestSample();
                        }
                    },

                    error => {
                        if (
                            finished ||
                            !sessionIsStillActive()
                        ) {
                            return;
                        }

                        console.error(
                            "Location error:",
                            error
                        );

                        if (
                            samples.length > 0
                        ) {
                            finishWithBestSample();
                            return;
                        }

                        finished = true;
                        stopLocationWatch();

                        if (
                            !sessionIsStillActive()
                        ) {
                            return;
                        }

                        locationMode = "idle";
                        setLocateButtonBusy(false);

                        if (
                            error.code ===
                            error.PERMISSION_DENIED
                        ) {
                            setLocationMessage(
                                "Location permission was denied. Allow location access or select the point manually.",
                                true
                            );
                        }
                        else if (
                            error.code ===
                            error.POSITION_UNAVAILABLE
                        ) {
                            setLocationMessage(
                                "Your device cannot determine its location. Select the point manually.",
                                true
                            );
                        }
                        else if (
                            error.code ===
                            error.TIMEOUT
                        ) {
                            setLocationMessage(
                                "Location detection timed out. Try again or select the point manually.",
                                true
                            );
                        }
                        else {
                            setLocationMessage(
                                "Location detection failed. Select the point manually.",
                                true
                            );
                        }
                    },

                    {
                        enableHighAccuracy: true,

                        timeout:
                            LOCATION_CONFIG
                                .watchTimeoutMs,

                        maximumAge: 0
                    }
                );

        /*
         * Some browsers do not trigger the timeout
         * callback reliably, so this is a fallback.
         */
        window.setTimeout(
            () => {
                if (
                    !finished &&
                    sessionIsStillActive()
                ) {
                    finishWithBestSample();
                }
            },

            LOCATION_CONFIG
                .watchTimeoutMs +
            1000
        );
    }

    // ==================================================
    // LOCATION BUTTON EVENTS
    // ==================================================

    locateMeButton?.addEventListener(
        "click",
        useCurrentLocation
    );

    allowLocationButton?.addEventListener(
        "click",
        useCurrentLocation
    );

    skipLocationButton?.addEventListener(
        "click",
        () => {
            closeLocationModal();

            cancelAutomaticLocation();

            setLocationMessage(
                "Click the map or drag the marker to select your location manually. You can use Use my location at any time to return to your current position."
            );

            setTimeout(() => {
                map?.invalidateSize();
            }, 100);
        }
    );

    // ==================================================
    // FORM SUBMIT
    // ==================================================

    form.addEventListener(
        "submit",
        event => {
            console.log(
                "Create account clicked."
            );

            console.log(
                "Current step:",
                currentStep
            );

            console.log(
                "===== FORM VALUES ====="
            );

            console.log(
                "Username:",
                username?.value
            );

            console.log(
                "Email:",
                email?.value
            );

            console.log(
                "Phone:",
                phone?.value
            );

            console.log(
                "Address:",
                address?.value
            );

            console.log(
                "Latitude:",
                latitude?.value
            );

            console.log(
                "Longitude:",
                longitude?.value
            );

            console.log(
                "Password length:",
                (
                    password?.value || ""
                ).length
            );

            console.log(
                "Confirm password length:",
                (
                    confirmPassword?.value ||
                    ""
                ).length
            );

            console.log(
                "======================="
            );

            const step1Valid =
                validateStep1() &&
                availabilityChecked &&
                sameAvailabilityValues(
                    getAvailabilityValues(),
                    checkedAvailabilityValues
                );

            if (!step1Valid) {
                setAvailabilityStatus(
                    "Please press Check availability and pass the duplicate check before creating the account.",
                    "error"
                );
            }

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
                }
                else if (!step2Valid) {
                    showStep(2);
                }
                else {
                    showStep(3);
                }

                return;
            }

            closeLocationModal();

            locationMode = "idle";
            locationSessionNumber++;
            stopLocationWatch();

            if (submitButton) {
                submitButton.disabled =
                    true;

                submitButton.textContent =
                    "Creating account…";
            }
        }
    );

    // ==================================================
    // CLEAN UP WHEN PAGE CLOSES
    // ==================================================

    window.addEventListener(
        "beforeunload",
        () => {
            locationMode = "idle";
            locationSessionNumber++;
            stopLocationWatch();
        }
    );

    // ==================================================
    // INITIAL STATE
    // ==================================================

    showStep(currentStep);

    updatePasswordRules();
    updateAvailabilityButton();

    if (availabilityInputsAreComplete()) {
        setAvailabilityStatus(
            "Press Check availability to continue.",
            "idle"
        );
    }
    else {
        setAvailabilityStatus(
            "Complete the username, Gmail address and phone number first.",
            "idle"
        );
    }
})();