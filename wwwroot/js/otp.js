(() => {
    "use strict";

    const config =
        window.otpPage || {};

    const timer =
        document.getElementById("otpTimer");

    const otp =
        document.getElementById("otp");

    const resendButton =
        document.getElementById("resendButton");

    let asked = false;

    // ==================================================
    // OTP INPUT
    // Only allow 6 digits
    // ==================================================

    otp?.addEventListener("input", () => {

        otp.value =
            otp.value
                .replace(/\D/g, "")
                .slice(0, 6);

    });

    // ==================================================
    // GET OTP EXPIRY TIME FROM SERVER
    // ==================================================

    const expiresAt =
        Date.parse(
            config.expiresAtUtc || ""
        );

    // ==================================================
    // UPDATE TIMER COLOUR
    // 60–40 seconds: cyan
    // 39–15 seconds: yellow
    // 14–0 seconds: red
    // ==================================================

    function updateTimerColour(seconds) {

        if (!timer) {
            return;
        }

        if (seconds >= 40) {

            timer.style.color =
                "#06b6d4";

            timer.classList.remove(
                "danger-blink"
            );

        }
        else if (seconds >= 15) {

            timer.style.color =
                "#f59e0b";

            timer.classList.remove(
                "danger-blink"
            );

        }
        else {

            timer.style.color =
                "#ef4444";

            timer.classList.add(
                "danger-blink"
            );

        }

    }

    // ==================================================
    // COUNTDOWN
    // ==================================================

    function updateTimer() {

        /*
         * If the server did not provide a valid expiry
         * time, do not immediately treat the OTP as expired.
         */
        if (!Number.isFinite(expiresAt)) {

            if (timer) {

                timer.textContent =
                    "01:00";

                timer.style.color =
                    "#06b6d4";

                timer.classList.remove(
                    "danger-blink"
                );

            }

            console.error(
                "OTP expiry time is missing or invalid:",
                config.expiresAtUtc
            );

            return;
        }

        const seconds =
            Math.max(
                0,
                Math.ceil(
                    (
                        expiresAt -
                        Date.now()
                    ) / 1000
                )
            );

        // ==================================================
        // DISPLAY TIME
        // ==================================================

        if (timer) {

            const minute =
                Math.floor(
                    seconds / 60
                );

            const second =
                seconds % 60;

            timer.textContent =
                `${String(minute).padStart(2, "0")}:${String(second).padStart(2, "0")}`;

        }

        // Change colour before returning
        updateTimerColour(seconds);

        // ==================================================
        // CONTINUE COUNTDOWN
        // ==================================================

        if (seconds > 0) {

            setTimeout(
                updateTimer,
                1000
            );

            return;
        }

        // ==================================================
        // OTP EXPIRED
        // ==================================================

        if (
            resendButton &&
            Number(
                config.resendRemaining
            ) > 0
        ) {

            resendButton.disabled =
                false;

        }

        /*
         * Prevent multiple confirm boxes from appearing.
         */
        if (asked) {
            return;
        }

        asked = true;

        // ==================================================
        // NO RESEND ATTEMPTS LEFT
        // ==================================================

        if (
            Number(
                config.resendRemaining
            ) <= 0
        ) {

            alert(
                "You have used all resend attempts. " +
                "Please return to the Register page " +
                "and check your Gmail address."
            );

            return;
        }

        // ==================================================
        // ASK USER TO RESEND
        // ==================================================

        const resend =
            confirm(
                "Your OTP has expired.\n\n" +
                "Do you want to resend a new OTP?"
            );

        if (resend) {

            const resendForm =
                document.getElementById(
                    config.resendFormId
                );

            if (resendForm) {

                resendForm.submit();

            }
            else {

                console.error(
                    "Resend OTP form could not be found."
                );

            }

            return;
        }

        // ==================================================
        // CANCEL REGISTRATION
        // ==================================================

        alert(
            "You will return to the Register page. " +
            "Please check or change your Gmail address."
        );

        const cancelForm =
            document.getElementById(
                config.cancelFormId
            );

        if (cancelForm) {

            cancelForm.submit();

        }
        else {

            console.error(
                "Cancel OTP form could not be found."
            );

        }

    }

    // ==================================================
    // START TIMER
    // ==================================================

    updateTimer();

})();