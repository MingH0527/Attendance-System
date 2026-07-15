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

    if (!registerButton) {
        console.error(
            "Register Passkey button was not found."
        );

        return;
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

    function prepareCreationOptions(options) {
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

    function credentialToJson(credential) {
        return {
            id: credential.id,

            rawId:
                arrayBufferToBase64Url(
                    credential.rawId
                ),

            type: credential.type,

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
                        .getTransports === "function"
                        ? credential.response
                            .getTransports()
                        : []
            },

            clientExtensionResults:
                credential
                    .getClientExtensionResults(),

            authenticatorAttachment:
                credential.authenticatorAttachment
        };
    }

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
    }

    function setLoading(isLoading) {
        registerButton.disabled = isLoading;

        if (registerButtonText) {
            registerButtonText.textContent =
                isLoading
                    ? "Waiting for verification..."
                    : "Register Passkey";
        }
    }

    registerButton.addEventListener(
        "click",
        async () => {
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

            setLoading(true);

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
                    await navigator.credentials.create({
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

                            body: JSON.stringify(
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
                    window.location.reload();
                }, 1000);
            }
            catch (error) {
                console.error(error);

                let message =
                    error.message ||
                    "Passkey registration failed.";

                if (error.name === "NotAllowedError") {
                    message =
                        "Passkey registration was cancelled " +
                        "or timed out.";
                }

                if (error.name === "InvalidStateError") {
                    message =
                        "This passkey is already registered.";
                }

                showMessage(
                    message,
                    false
                );

                setLoading(false);
            }
        }
    );
})();