(() => {
    "use strict";

    const button =
        document.getElementById(
            "verifyBindingButton"
        );

    const message =
        document.getElementById(
            "bindingMessage"
        );

    const token =
        document.getElementById(
            "bindingToken"
        )?.value;

    const antiForgeryToken =
        document.querySelector(
            '#bindingForm input[name="__RequestVerificationToken"]'
        )?.value;

    function getOrCreateDeviceIdentifier() {
        const storageKey =
            "WebAndMobile.PasskeyPhoneDeviceId";

        let value =
            window.localStorage.getItem(
                storageKey
            );

        if (value &&
            /^[a-f0-9]{32}$/i.test(value)) {
            return value.toLowerCase();
        }

        value =
            window.crypto
                .randomUUID()
                .replace(/-/g, "")
                .toLowerCase();

        window.localStorage.setItem(
            storageKey,
            value
        );

        return value;
    }

    async function verify() {
        if (!button ||
            !token ||
            !antiForgeryToken) {
            return;
        }

        button.disabled = true;
        button.textContent =
            "Verifying...";

        const formData =
            new FormData();

        formData.append(
            "token",
            token
        );

        formData.append(
            "deviceIdentifier",
            getOrCreateDeviceIdentifier()
        );

        formData.append(
            "__RequestVerificationToken",
            antiForgeryToken
        );

        try {
            const response =
                await fetch(
                    "/PasskeyDeviceBinding/CompleteBinding",
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
                    "Device verification failed."
                );
            }

            message.textContent =
                data.warningMessage
                    ? `${data.message} ${data.warningMessage}`
                    : data.message;

            button.textContent =
                "Device verified";
        }
        catch (error) {
            message.textContent =
                error.message ||
                "Device verification failed.";

            button.disabled = false;
            button.textContent =
                "Try again";
        }
    }

    button?.addEventListener(
        "click",
        verify
    );
})();
