document.addEventListener("DOMContentLoaded", () => {

    const pinInput = document.getElementById("pin");
    const toggleButton = document.getElementById("togglePinButton");
    const form = document.getElementById("verifyAdminPinForm");
    const clientError = document.getElementById("clientError");

    if (toggleButton && pinInput) {

        toggleButton.addEventListener("click", () => {

            const hidden = pinInput.type === "password";

            pinInput.type = hidden ? "text" : "password";
            toggleButton.textContent = hidden ? "Hide" : "Show";

        });

    }

    if (pinInput) {

        pinInput.addEventListener("input", () => {

            pinInput.value = pinInput.value
                .replace(/\D/g, "")
                .slice(0, 6);

            if (clientError) {

                clientError.hidden = true;
                clientError.textContent = "";

            }

        });

    }

    if (form) {

        form.addEventListener("submit", (event) => {

            const pin = pinInput.value.trim();

            if (!/^\d{6}$/.test(pin)) {

                event.preventDefault();

                clientError.hidden = false;
                clientError.textContent =
                    "Please enter a valid 6-digit PIN.";

                pinInput.focus();

            }

        });

    }

});