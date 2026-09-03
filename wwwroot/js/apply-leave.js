const startDate = document.getElementById("startDate");
const endDate = document.getElementById("endDate");

function updateEndDateLimit() {
    if (!startDate || !endDate || !startDate.value) {
        return;
    }

    const start = new Date(startDate.value + "T00:00:00");

    const maxDate = new Date(start);

    maxDate.setDate(maxDate.getDate() + 6);

    const year = maxDate.getFullYear();

    const month =
        String(maxDate.getMonth() + 1)
            .padStart(2, "0");

    const day =
        String(maxDate.getDate())
            .padStart(2, "0");

    const maxDateValue =
        `${year}-${month}-${day}`;

    endDate.min = startDate.value;

    endDate.max = maxDateValue;

 
    if (
        endDate.value < startDate.value ||
        endDate.value > maxDateValue
    ) {
        endDate.value = startDate.value;
    }
}

startDate?.addEventListener(
    "change",
    updateEndDateLimit
);

updateEndDateLimit();