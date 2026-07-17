(() => {
    "use strict";

    const registerButton =
        document.getElementById(
            "registerPasskeyButton"
        );

    const registerButtonText =
        document.getElementById(
            "registerButtonText"
        );

    const messageBox =
        document.getElementById(
            "passkeyMessage"
        );

    const tokenInput =
        document.querySelector(
            '#passkeyTokenForm ' +
            'input[name="__RequestVerificationToken"]'
        );

    function showMessage(
        message,
        isSuccess
    ) {
        if (!messageBox) {
            alert(message);
            return;
        }

        messageBox.hidden = false;
        messageBox.textContent = message;

        messageBox.classList.remove(
            "success",
            "error"
        );

        messageBox.classList.add(
            isSuccess
                ? "success"
                : "error"
        );

        messageBox.scrollIntoView({
            behavior: "smooth",
            block: "center"
        });
    }

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
            new Uint8Array(
                binary.length
            );

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

    function arrayBufferToBase64Url(
        buffer
    ) {
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

    function prepareCreationOptions(
        options
    ) {
        options.challenge =
            base64UrlToUint8Array(
                options.challenge
            );

        options.user.id =
            base64UrlToUint8Array(
                options.user.id
            );

        if (
            Array.isArray(
                options.excludeCredentials
            )
        ) {
            options.excludeCredentials =
                options.excludeCredentials.map(
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

    function credentialToJson(
        credential
    ) {
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
                attestationObject:
                    arrayBufferToBase64Url(
                        credential.response
                            .attestationObject
                    ),

                clientDataJSON:
                    arrayBufferToBase64Url(
                        credential.response
                            .clientDataJSON
                    ),

                transports:
                    typeof credential.response
                        .getTransports ===
                        "function"
                        ? credential.response
                            .getTransports()
                        : []
            },

            clientExtensionResults:
                credential
                    .getClientExtensionResults(),

            authenticatorAttachment:
                credential
                    .authenticatorAttachment
        };
    }

    function setRegisterLoading(
        isLoading
    ) {
        if (!registerButton) {
            return;
        }

        registerButton.disabled =
            isLoading;

        if (registerButtonText) {
            registerButtonText.textContent =
                isLoading
                    ? "Waiting for verification..."
                    : "Register Passkey";
        }
    }

    async function registerPasskey() {
        if (
            !window.PublicKeyCredential ||
            !navigator.credentials
        ) {
            showMessage(
                "This browser does not support passkeys.",
                false
            );

            return;
        }

        if (!tokenInput) {
            showMessage(
                "Security token could not be found.",
                false
            );

            return;
        }

        setRegisterLoading(true);

        try {
            const optionResponse =
                await fetch(
                    "/MyProfile/CreatePasskeyOptions",
                    {
                        method: "POST",

                        headers: {
                            "RequestVerificationToken":
                                tokenInput.value
                        }
                    }
                );

            const optionData =
                await optionResponse.json();

            if (!optionResponse.ok) {
                throw new Error(
                    optionData.message ||
                    "Unable to start passkey registration."
                );
            }

            const publicKeyOptions =
                prepareCreationOptions(
                    optionData
                );

            const credential =
                await navigator.credentials
                    .create({
                        publicKey:
                            publicKeyOptions
                    });

            if (!credential) {
                throw new Error(
                    "No passkey credential was created."
                );
            }

            const credentialJson =
                credentialToJson(
                    credential
                );

            const completeResponse =
                await fetch(
                    "/MyProfile/CompletePasskeyRegistration",
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
                                credentialJson
                            )
                    }
                );

            const completeData =
                await completeResponse.json();

            if (!completeResponse.ok) {
                throw new Error(
                    completeData.message ||
                    "Passkey verification failed."
                );
            }

            showMessage(
                completeData.message,
                true
            );

            setTimeout(() => {
                window.location.href =
                    completeData.redirectUrl ||
                    "/MyProfile/Passkey";
            }, 900);
        }
        catch (error) {
            console.error(error);

            let message =
                error.message ||
                "Passkey registration failed.";

            if (
                error.name ===
                "NotAllowedError"
            ) {
                message =
                    "Passkey registration was " +
                    "cancelled or timed out.";
            }

            if (
                error.name ===
                "InvalidStateError"
            ) {
                message =
                    "This passkey is already registered.";
            }

            showMessage(
                message,
                false
            );

            setRegisterLoading(false);
        }
    }

    async function deletePasskey(
        button
    ) {
        if (!tokenInput) {
            showMessage(
                "Security token could not be found.",
                false
            );

            return;
        }

        const passkeyId =
            button.dataset.passkeyId;

        const deviceName =
            button.dataset.deviceName ||
            "this passkey";

        const isLast =
            button.dataset.isLast ===
            "true";

        const isAdmin =
            button.dataset.isAdmin ===
            "true";

        let confirmationMessage =
            `Delete "${deviceName}"?`;

        if (isAdmin && isLast) {
            confirmationMessage =
                "This is your final Admin passkey.\n\n" +
                "Deleting it will also remove your " +
                "Admin Security PIN configuration.\n\n" +
                "You must register a new passkey and " +
                "set a new PIN before using secure " +
                "Admin Passkey Login again.\n\n" +
                "Continue?";
        }
        else {
            confirmationMessage +=
                "\n\nThis passkey will no longer " +
                "work for this website.";
        }

        const confirmed =
            window.confirm(
                confirmationMessage
            );

        if (!confirmed) {
            return;
        }

        button.disabled = true;
        button.textContent =
            "Deleting...";

        try {
            const formData =
                new FormData();

            formData.append(
                "passkeyId",
                passkeyId
            );

            formData.append(
                "__RequestVerificationToken",
                tokenInput.value
            );

            const response =
                await fetch(
                    "/MyProfile/DeletePasskey",
                    {
                        method: "POST",
                        body: formData
                    }
                );

            const data =
                await response.json();

            if (!response.ok) {
                throw new Error(
                    data.message ||
                    "Unable to delete the passkey."
                );
            }

            showMessage(
                data.message,
                true
            );

            setTimeout(() => {
                window.location.reload();
            }, 800);
        }
        catch (error) {
            console.error(error);

            showMessage(
                error.message ||
                "Passkey deletion failed.",
                false
            );

            button.disabled = false;
            button.textContent =
                "Delete";
        }
    }

    registerButton?.addEventListener(
        "click",
        registerPasskey
    );

    document
        .querySelectorAll(
            ".delete-passkey-button"
        )
        .forEach(button => {
            button.addEventListener(
                "click",
                () =>
                    deletePasskey(
                        button
                    )
            );
        });
})();