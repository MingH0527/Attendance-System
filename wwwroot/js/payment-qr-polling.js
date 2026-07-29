(() => {
    "use strict";

    const app = document.getElementById("qrPayment");
    if (!app) return;

    const statusElement = document.getElementById("qrStatus");
    const statusUrl = app.dataset.statusUrl;
    const successUrl = app.dataset.successUrl;

    async function checkStatus() {
        try {
            const response = await fetch(statusUrl, {
                headers: { "Accept": "application/json" },
                cache: "no-store"
            });

            if (!response.ok) throw new Error("Unable to check payment status.");
            const data = await response.json();

            if (data.status === "Paid") {
                statusElement.className = "waiting-status paid";
                statusElement.textContent = "Payment confirmed. Opening receipt…";
                window.location.assign(successUrl);
                return;
            }

            if (data.status === "Expired") {
                statusElement.className = "waiting-status expired";
                statusElement.textContent = "QR code expired. Return and generate a new one.";
                return;
            }

            window.setTimeout(checkStatus, 2000);
        } catch (error) {
            statusElement.textContent = error.message;
            window.setTimeout(checkStatus, 3000);
        }
    }

    window.setTimeout(checkStatus, 1200);
})();
