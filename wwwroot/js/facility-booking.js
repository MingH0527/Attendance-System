(() => {
    "use strict";

    const panel = document.getElementById("bookingPanel");
    const slots = Array.from(document.querySelectorAll(".time-slot.available"));
    const allSlots = Array.from(document.querySelectorAll(".time-slot"));
    const startInput = document.getElementById("startHour");
    const endInput = document.getElementById("endHour");
    const selectedTime = document.getElementById("selectedTime");
    const selectedDuration = document.getElementById("selectedDuration");
    const bookButton = document.getElementById("bookButton");
    const clientError = document.getElementById("bookingClientError");

    const activeBookings = Number(panel?.dataset.activeBookings || 0);
    const bookingLimit = Number(panel?.dataset.bookingLimit || 2);
    const maxSingleBookingHours = 6;

    let firstHour = null;
    let secondHour = null;

    function hourText(hour) {
        return hour === 24 ? "00:00" : `${String(hour).padStart(2, "0")}:00`;
    }

    function setError(message) {
        if (!clientError) return;
        clientError.textContent = message || "";
        clientError.hidden = !message;
    }

    function clearSelection() {
        allSlots.forEach(slot => slot.classList.remove("selected-range", "selected-boundary"));
    }

    function rangeContainsBlocked(start, end) {
        for (let hour = start; hour < end; hour += 2) {
            const slot = allSlots.find(item => Number(item.dataset.hour) === hour);
            if (!slot || slot.disabled) return true;
        }
        return false;
    }

    function resetToFirstSelection(message) {
        secondHour = null;
        setError(message);
        render(false);
    }

    function render(clearMessage = true) {
        clearSelection();
        if (clearMessage) setError("");

        if (activeBookings >= bookingLimit) {
            startInput.value = "";
            endInput.value = "";
            selectedTime.textContent = "Booking limit reached";
            selectedDuration.textContent = `${activeBookings} / ${bookingLimit} Active`;
            bookButton.disabled = true;
            setError("You currently have 2 active facility bookings. Complete or cancel an existing booking before making another booking.");
            return;
        }

        if (firstHour === null) {
            startInput.value = "";
            endInput.value = "";
            selectedTime.textContent = "Not selected";
            selectedDuration.textContent = "0 Hours";
            bookButton.disabled = true;
            return;
        }

        if (secondHour === null) {
            const firstSlot = allSlots.find(item => Number(item.dataset.hour) === firstHour);
            firstSlot?.classList.add("selected-boundary");
            startInput.value = firstHour;
            endInput.value = firstHour + 2;
            selectedTime.textContent = `${hourText(firstHour)} – ${hourText(firstHour + 2)}`;
            selectedDuration.textContent = "2 Hours";
            bookButton.disabled = false;
            return;
        }

        const start = Math.min(firstHour, secondHour);
        const end = Math.max(firstHour, secondHour) + 2;
        const duration = end - start;

        if (rangeContainsBlocked(start, end)) {
            resetToFirstSelection("The selected range contains an unavailable or already-booked time slot.");
            return;
        }

        if (duration > maxSingleBookingHours) {
            resetToFirstSelection("A single facility booking can be a maximum of 6 hours (3 continuous slots).");
            return;
        }

        for (let hour = start; hour < end; hour += 2) {
            const slot = allSlots.find(item => Number(item.dataset.hour) === hour);
            if (!slot) continue;
            slot.classList.add(hour === start || hour === end - 2 ? "selected-boundary" : "selected-range");
        }

        startInput.value = start;
        endInput.value = end;
        selectedTime.textContent = `${hourText(start)} – ${hourText(end)}`;
        selectedDuration.textContent = `${duration} Hours`;
        bookButton.disabled = false;
    }

    slots.forEach(slot => {
        slot.addEventListener("click", () => {
            const hour = Number(slot.dataset.hour);
            if (firstHour === null || secondHour !== null) {
                firstHour = hour;
                secondHour = null;
            } else if (hour === firstHour) {
                firstHour = null;
            } else {
                secondHour = hour;
            }
            render();
        });
    });

    document.querySelectorAll(".facility-thumb").forEach(button => {
        button.addEventListener("click", () => {
            const main = document.getElementById("mainFacilityImage");
            if (main && button.dataset.image) main.src = button.dataset.image;
        });
    });

    render();
})();
