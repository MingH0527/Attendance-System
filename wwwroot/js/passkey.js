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


    function getOrCreateDeviceIdentifier() {
        const storageKey =
            "WebAndMobile.PasskeyDeviceId";

        let deviceIdentifier =
            window.localStorage.getItem(
                storageKey
            );

        if (
            deviceIdentifier &&
            /^[a-f0-9]{32}$/i.test(
                deviceIdentifier
            )
        ) {
            return deviceIdentifier
                .toLowerCase();
        }

        if (
            window.crypto &&
            typeof window.crypto.randomUUID ===
                "function"
        ) {
            deviceIdentifier =
                window.crypto
                    .randomUUID()
                    .replace(/-/g, "");
        }
        else {
            const bytes =
                new Uint8Array(16);

            window.crypto
                .getRandomValues(bytes);

            deviceIdentifier =
                Array.from(bytes)
                    .map(
                        value =>
                            value
                                .toString(16)
                                .padStart(2, "0")
                    )
                    .join("");
        }

        window.localStorage.setItem(
            storageKey,
            deviceIdentifier
        );

        return deviceIdentifier;
    }

    function getBasicDeviceType(userAgent) {
        const ua = userAgent || "";

        const isTablet =
            /iPad|Tablet|PlayBook|Silk/i.test(ua) ||
            (/Android/i.test(ua) &&
             !/Mobile/i.test(ua)) ||
            /SM-T|SM-X/i.test(ua);

        if (isTablet) {
            return "Tablet";
        }

        const isMobile =
            /Mobi|Android|iPhone|iPod|Windows Phone/i
                .test(ua);

        return isMobile
            ? "Mobile"
            : "Desktop";
    }

    function getOperatingSystem(userAgent) {
        const ua = userAgent || "";

        let match =
            ua.match(/Android\s([0-9.]+)/i);

        if (match) {
            return `Android ${match[1]}`;
        }

        match =
            ua.match(/(?:iPhone OS|CPU OS)\s([0-9_]+)/i);

        if (match) {
            return `iOS ${match[1].replace(/_/g, ".")}`;
        }

        if (/Windows NT 10\.0/i.test(ua)) {
            return "Windows";
        }

        if (/Mac OS X/i.test(ua)) {
            match =
                ua.match(/Mac OS X\s([0-9_]+)/i);

            return match
                ? `macOS ${match[1].replace(/_/g, ".")}`
                : "macOS";
        }

        if (/Linux/i.test(ua)) {
            return "Linux";
        }

        return "Unknown OS";
    }

    function getBrowser(userAgent) {
        const ua = userAgent || "";

        let match =
            ua.match(/Edg\/([0-9.]+)/i);

        if (match) {
            return `Microsoft Edge ${match[1]}`;
        }

        match =
            ua.match(/Chrome\/([0-9.]+)/i);

        if (match && !/Edg\//i.test(ua)) {
            return `Google Chrome ${match[1]}`;
        }

        match =
            ua.match(/Firefox\/([0-9.]+)/i);

        if (match) {
            return `Mozilla Firefox ${match[1]}`;
        }

        match =
            ua.match(/Version\/([0-9.]+).*Safari/i);

        if (match && !/Chrome/i.test(ua)) {
            return `Safari ${match[1]}`;
        }

        return "Unknown Browser";
    }

    async function getDeviceInfo(
        credential
    ) {
        const userAgent =
            navigator.userAgent || "";

        let deviceType =
            getBasicDeviceType(
                userAgent
            );

        let operatingSystem =
            getOperatingSystem(
                userAgent
            );

        let browser =
            getBrowser(
                userAgent
            );

        let deviceName = "";

        if (navigator.userAgentData) {
            try {
                const highEntropy =
                    await navigator.userAgentData
                        .getHighEntropyValues([
                            "model",
                            "platform",
                            "platformVersion",
                            "fullVersionList"
                        ]);

                if (highEntropy.model) {
                    deviceName =
                        highEntropy.model;
                }

                if (navigator.userAgentData.mobile &&
                    deviceType !== "Tablet") {
                    deviceType = "Mobile";
                }

                if (
                    highEntropy.platform &&
                    highEntropy.platformVersion
                ) {
                    operatingSystem =
                        `${highEntropy.platform} ` +
                        `${highEntropy.platformVersion}`;
                }

                if (
                    Array.isArray(
                        highEntropy.fullVersionList)
                ) {
                    const preferred =
                        highEntropy.fullVersionList
                            .find(
                                item =>
                                    /Google Chrome|Microsoft Edge|Chromium/i
                                        .test(
                                            item.brand
                                        ));

                    if (preferred) {
                        browser =
                            `${preferred.brand} ` +
                            `${preferred.version}`;
                    }
                }
            }
            catch (error) {
                console.debug(
                    "High entropy device information unavailable.",
                    error
                );
            }
        }

        if (!deviceName) {
            if (deviceType === "Tablet") {
                deviceName =
                    /iPad/i.test(userAgent)
                        ? "iPad"
                        : "Tablet";
            }
            else if (deviceType === "Mobile") {
                deviceName =
                    /iPhone/i.test(userAgent)
                        ? "iPhone"
                        : "Mobile Phone";
            }
            else if (/Windows/i.test(userAgent)) {
                deviceName = "Windows PC";
            }
            else if (/Macintosh/i.test(userAgent)) {
                deviceName = "Mac";
            }
            else {
                deviceName = "Computer";
            }
        }

        return {
            deviceIdentifier:
                getOrCreateDeviceIdentifier(),
            deviceName,
            deviceType,
            operatingSystem,
            browser,

            authenticatorAttachment:
                credential
                    .authenticatorAttachment ||
                "Unknown",

            transports:
                typeof credential.response
                    .getTransports === "function"
                    ? credential.response
                        .getTransports()
                    : [],

            userAgent
        };
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

    async function readJsonResponse(
        response
    ) {
        const text =
            await response.text();

        if (!text) {
            return {};
        }

        try {
            return JSON.parse(text);
        }
        catch {
            throw new Error(
                text.length > 300
                    ? text.substring(0, 300)
                    : text
            );
        }
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

    const bindingModal =
        document.getElementById(
            "deviceBindingModal"
        );

    const bindingQr =
        document.getElementById(
            "deviceBindingQr"
        );

    const bindingStatus =
        document.getElementById(
            "deviceBindingStatus"
        );
    const cancelBindingButton =
        document.getElementById(
            "cancelDeviceBindingButton"
        );

    let bindingCancelled = false;

    function closeBindingModal() {
        bindingCancelled = true;

        if (bindingModal) {
            bindingModal.hidden = true;
        }
    }

    cancelBindingButton?.addEventListener(
        "click",
        closeBindingModal
    );

    async function startPhoneDeviceBinding() {
        bindingCancelled = false;

        const response =
            await fetch(
                "/PasskeyDeviceBinding/Start",
                {
                    method: "POST",
                    headers: {
                        "RequestVerificationToken":
                            tokenInput.value
                    }
                }
            );

        const data =
            await readJsonResponse(
                response
            );

        if (!response.ok) {
            throw new Error(
                data.message ||
                "Unable to start phone verification."
            );
        }

        if (bindingQr) {
            bindingQr.src = data.qrUrl;
        }
        if (bindingStatus) {
            bindingStatus.textContent =
                "Waiting for the phone or tablet to verify...";
        }

        if (bindingModal) {
            bindingModal.hidden = false;
        }

        const startedAt =
            Date.now();

        while (!bindingCancelled) {
            if (
                Date.now() - startedAt >
                5 * 60 * 1000
            ) {
                throw new Error(
                    "Device verification expired. Please try again."
                );
            }

            await new Promise(
                resolve =>
                    setTimeout(
                        resolve,
                        1500
                    )
            );

            const statusResponse =
                await fetch(
                    "/PasskeyDeviceBinding/Status" +
                    `?token=${encodeURIComponent(data.token)}`,
                    {
                        method: "GET",
                        cache: "no-store"
                    }
                );

            const statusData =
                await readJsonResponse(
                    statusResponse
                );

            if (!statusResponse.ok) {
                throw new Error(
                    statusData.message ||
                    "Unable to check device verification."
                );
            }

            if (statusData.status === "Verified") {
                if (bindingStatus) {
                    bindingStatus.textContent =
                        "Device verified. Continuing Passkey registration...";
                }

                await new Promise(
                    resolve =>
                        setTimeout(
                            resolve,
                            500
                        )
                );

                if (bindingModal) {
                    bindingModal.hidden = true;
                }

                return;
            }

            if (statusData.status === "Blocked") {
                throw new Error(
                    "This phone or tablet has reached its 4-account Passkey limit."
                );
            }

            if (
                statusData.status === "Expired" ||
                statusData.status === "Cancelled"
            ) {
                throw new Error(
                    "Device verification expired or was cancelled."
                );
            }
        }

        throw new Error(
            "Device verification was cancelled."
        );
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
            await startPhoneDeviceBinding();

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
                await readJsonResponse(
                    optionResponse
                );

            if (!optionResponse.ok) {
                throw new Error(
                    optionData.message ||
                    "Unable to start passkey registration."
                );
            }

            if (optionData.warningMessage) {
                const proceed =
                    window.confirm(
                        optionData.warningMessage +
                        "\n\nContinue with Passkey registration?"
                    );

                if (!proceed) {
                    setRegisterLoading(false);
                    return;
                }
            }

            const publicKeyOptions =
                prepareCreationOptions(
                    optionData.options
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

            const deviceInfo =
                await getDeviceInfo(
                    credential
                );

            const registrationRequest = {
                attestationResponse:
                    credentialJson,
                deviceInfo
            };

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
                                registrationRequest
                            )
                    }
                );

            const completeData =
                await readJsonResponse(
                    completeResponse
                );

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

            if (bindingModal) {
                bindingModal.hidden = true;
            }

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
                await readJsonResponse(
                    response
                );

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