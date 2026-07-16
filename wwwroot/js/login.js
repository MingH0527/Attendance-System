(() => {
    "use strict";

    const loginForm =
        document.getElementById("loginForm");

    const usernameInput =
        document.getElementById("username");

    const passwordInput =
        document.getElementById("password");

    const usernameError =
        document.getElementById("usernameError");

    const passwordError =
        document.getElementById("passwordError");

    const passwordToggle =
        document.getElementById("passwordToggle");

    const loginButton =
        document.getElementById("loginButton");

    const usernameLoginTab =
        document.getElementById("usernameLoginTab");

    const passkeyLoginTab =
        document.getElementById("passkeyLoginTab");

    const usernameLoginPanel =
        document.getElementById("usernameLoginPanel");

    const passkeyLoginPanel =
        document.getElementById("passkeyLoginPanel");

    const passkeyLoginButton =
        document.getElementById(
            "startPasskeyLoginButton"
        );

    const passkeyLoginButtonText =
        document.getElementById(
            "passkeyLoginButtonText"
        );

    const passkeyMessage =
        document.getElementById(
            "passkeyLoginMessage"
        );

    const tokenInput =
        document.querySelector(
            '#passkeyLoginTokenForm ' +
            'input[name="__RequestVerificationToken"]'
        );

    // ==================================================
    // LOGIN METHOD TABS
    // ==================================================

    function showUsernameLogin() {
        usernameLoginTab?.classList.add("active");
        passkeyLoginTab?.classList.remove("active");

        if (usernameLoginPanel) {
            usernameLoginPanel.hidden = false;
        }

        if (passkeyLoginPanel) {
            passkeyLoginPanel.hidden = true;
        }

        hidePasskeyMessage();
    }

    function showPasskeyLogin() {
        passkeyLoginTab?.classList.add("active");
        usernameLoginTab?.classList.remove("active");

        if (passkeyLoginPanel) {
            passkeyLoginPanel.hidden = false;
        }

        if (usernameLoginPanel) {
            usernameLoginPanel.hidden = true;
        }

        hidePasskeyMessage();
    }

    usernameLoginTab?.addEventListener(
        "click",
        showUsernameLogin
    );

    passkeyLoginTab?.addEventListener(
        "click",
        showPasskeyLogin
    );

    // ==================================================
    // PASSWORD SHOW / HIDE
    // ==================================================

    passwordToggle?.addEventListener(
        "click",
        () => {
            if (!passwordInput) {
                return;
            }

            const passwordIsVisible =
                passwordInput.type === "text";

            passwordInput.type =
                passwordIsVisible
                    ? "password"
                    : "text";

            passwordToggle.textContent =
                passwordIsVisible
                    ? "👁️"
                    : "🙈";

            passwordToggle.setAttribute(
                "aria-label",
                passwordIsVisible
                    ? "Show password"
                    : "Hide password"
            );
        }
    );

    // ==================================================
    // USERNAME LOGIN VALIDATION
    // ==================================================

    function setFieldState(
        input,
        errorElement,
        message
    ) {
        if (!input || !errorElement) {
            return false;
        }

        const hasError =
            Boolean(message);

        input.classList.toggle(
            "input-error",
            hasError
        );

        input.classList.toggle(
            "input-valid",
            !hasError &&
            input.value.trim().length > 0
        );

        errorElement.textContent =
            message || "";

        return !hasError;
    }

    function validateUsername() {
        const value =
            usernameInput?.value.trim() || "";

        if (!value) {
            return setFieldState(
                usernameInput,
                usernameError,
                "Username is required."
            );
        }

        return setFieldState(
            usernameInput,
            usernameError,
            ""
        );
    }

    function validatePassword() {
        const value =
            passwordInput?.value || "";

        if (!value) {
            return setFieldState(
                passwordInput,
                passwordError,
                "Password is required."
            );
        }

        return setFieldState(
            passwordInput,
            passwordError,
            ""
        );
    }

    usernameInput?.addEventListener(
        "blur",
        validateUsername
    );

    passwordInput?.addEventListener(
        "blur",
        validatePassword
    );

    usernameInput?.addEventListener(
        "input",
        () => {
            if (
                usernameInput.classList
                    .contains("input-error")
            ) {
                validateUsername();
            }
        }
    );

    passwordInput?.addEventListener(
        "input",
        () => {
            if (
                passwordInput.classList
                    .contains("input-error")
            ) {
                validatePassword();
            }
        }
    );

    loginForm?.addEventListener(
        "submit",
        event => {
            const usernameValid =
                validateUsername();

            const passwordValid =
                validatePassword();

            if (
                !usernameValid ||
                !passwordValid
            ) {
                event.preventDefault();
                return;
            }

            if (loginButton) {
                loginButton.disabled = true;
                loginButton.textContent =
                    "Logging in...";
            }
        }
    );

    // ==================================================
    // BASE64URL HELPERS
    // ==================================================

    function base64UrlToUint8Array(value) {
        if (!value) {
            return new Uint8Array();
        }

        const padding =
            "=".repeat(
                (4 - value.length % 4) % 4
            );

        const base64 =
            (value + padding)
                .replace(/-/g, "+")
                .replace(/_/g, "/");

        const binary =
            window.atob(base64);

        const bytes =
            new Uint8Array(binary.length);

        for (
            let index = 0;
            index < binary.length;
            index++
        ) {
            bytes[index] =
                binary.charCodeAt(index);
        }

        return bytes;
    }

    function arrayBufferToBase64Url(buffer) {
        if (!buffer) {
            return null;
        }

        const bytes =
            new Uint8Array(buffer);

        let binary = "";

        for (const byte of bytes) {
            binary +=
                String.fromCharCode(byte);
        }

        return window.btoa(binary)
            .replace(/\+/g, "-")
            .replace(/\//g, "_")
            .replace(/=+$/g, "");
    }

    // ==================================================
    // PREPARE ASSERTION OPTIONS
    // ==================================================

    function prepareAssertionOptions(options) {
        options.challenge =
            base64UrlToUint8Array(
                options.challenge
            );

        if (
            Array.isArray(
                options.allowCredentials
            )
        ) {
            options.allowCredentials =
                options.allowCredentials.map(
                    credential => ({
                        ...credential,

                        id:
                            base64UrlToUint8Array(
                                credential.id
                            )
                    })
                );
        }

        return options;
    }

    // ==================================================
    // CONVERT ASSERTION RESPONSE
    // ==================================================

    function assertionToJson(credential) {
        return {
            id:
                credential.id,

            rawId:
                arrayBufferToBase64Url(
                    credential.rawId
                ),

            type:
                credential.type,

            response: {
                authenticatorData:
                    arrayBufferToBase64Url(
                        credential.response
                            .authenticatorData
                    ),

                clientDataJSON:
                    arrayBufferToBase64Url(
                        credential.response
                            .clientDataJSON
                    ),

                signature:
                    arrayBufferToBase64Url(
                        credential.response
                            .signature
                    ),

                userHandle:
                    credential.response.userHandle
                        ? arrayBufferToBase64Url(
                            credential.response
                                .userHandle
                        )
                        : null
            },

            clientExtensionResults:
                credential
                    .getClientExtensionResults(),

            authenticatorAttachment:
                credential
                    .authenticatorAttachment
        };
    }

    // ==================================================
    // MESSAGE
    // ==================================================

    function showPasskeyMessage(
        message,
        success
    ) {
        if (!passkeyMessage) {
            alert(message);
            return;
        }

        passkeyMessage.hidden = false;
        passkeyMessage.textContent = message;

        passkeyMessage.classList.remove(
            "success-message",
            "error-message"
        );

        passkeyMessage.classList.add(
            success
                ? "success-message"
                : "error-message"
        );
    }

    function hidePasskeyMessage() {
        if (!passkeyMessage) {
            return;
        }

        passkeyMessage.hidden = true;
        passkeyMessage.textContent = "";

        passkeyMessage.classList.remove(
            "success-message",
            "error-message"
        );
    }

    function setPasskeyLoading(loading) {
        if (!passkeyLoginButton) {
            return;
        }

        passkeyLoginButton.disabled =
            loading;

        if (passkeyLoginButtonText) {
            passkeyLoginButtonText.textContent =
                loading
                    ? "Waiting for verification..."
                    : "Continue with Passkey";
        }
    }

    // ==================================================
    // PASSKEY LOGIN
    // ==================================================

    passkeyLoginButton?.addEventListener(
        "click",
        async () => {
            hidePasskeyMessage();

            if (
                !window.PublicKeyCredential ||
                !navigator.credentials
            ) {
                showPasskeyMessage(
                    "This browser does not support passkeys.",
                    false
                );

                return;
            }

            if (!tokenInput) {
                showPasskeyMessage(
                    "Security token could not be found.",
                    false
                );

                return;
            }

            setPasskeyLoading(true);

            try {
                const optionsResponse =
                    await fetch(
                        "/Passkey/CreateLoginOptions",
                        {
                            method: "POST",

                            headers: {
                                "RequestVerificationToken":
                                    tokenInput.value
                            }
                        }
                    );

                const optionsData =
                    await optionsResponse.json();

                if (!optionsResponse.ok) {
                    throw new Error(
                        optionsData.message ||
                        "Unable to start passkey login."
                    );
                }

                const publicKeyOptions =
                    prepareAssertionOptions(
                        optionsData
                    );

                const credential =
                    await navigator.credentials.get({
                        publicKey:
                            publicKeyOptions
                    });

                if (!credential) {
                    throw new Error(
                        "No passkey credential was returned."
                    );
                }

                const assertionJson =
                    assertionToJson(
                        credential
                    );

                const verificationResponse =
                    await fetch(
                        "/Passkey/CompleteLogin",
                        {
                            method: "POST",

                            headers: {
                                "Content-Type":
                                    "application/json",

                                "RequestVerificationToken":
                                    tokenInput.value
                            },

                            body:
                                JSON.stringify(
                                    assertionJson
                                )
                        }
                    );

                const verificationData =
                    await verificationResponse.json();

                if (!verificationResponse.ok) {
                    throw new Error(
                        verificationData.message ||
                        "Passkey verification failed."
                    );
                }

                showPasskeyMessage(
                    verificationData.message ||
                    "Login successful.",
                    true
                );

                window.setTimeout(
                    () => {
                        window.location.href =
                            verificationData.redirectUrl ||
                            "/Student/Index";
                    },
                    500
                );
            }
            catch (error) {
                console.error(error);

                let message =
                    error.message ||
                    "Passkey login failed.";

                if (
                    error.name ===
                    "NotAllowedError"
                ) {
                    message =
                        "Passkey login was cancelled, " +
                        "timed out, or no matching passkey was found.";
                }

                if (
                    error.name ===
                    "SecurityError"
                ) {
                    message =
                        "Passkey login was blocked because " +
                        "the website origin or HTTPS setting is incorrect.";
                }

                showPasskeyMessage(
                    message,
                    false
                );

                setPasskeyLoading(false);
            }
        }
    );
})();