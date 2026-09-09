document.addEventListener(
    "DOMContentLoaded",
    function () {

        const dataElement =
            document.getElementById(
                "attendance-chart-data"
            );


        if (!dataElement) {
            return;
        }


        const data =
            JSON.parse(
                dataElement.textContent
            );


        // ==========================================
        // COMMON FONT
        // ==========================================

        Chart.defaults.font.family =
            "Segoe UI, Arial, sans-serif";


        Chart.defaults.color =
            "#718096";



        // ==========================================
        // ATTENDANCE BY SUBJECT
        // ==========================================

        const subjectCanvas =
            document.getElementById(
                "subjectAttendanceChart"
            );


        if (
            subjectCanvas &&
            data.subjects
        ) {

            new Chart(
                subjectCanvas,
                {
                    type: "bar",

                    data: {
                        labels:
                            data.subjects.map(
                                x =>
                                    x.label
                            ),

                        datasets: [
                            {
                                label:
                                    "Attendance %",

                                data:
                                    data.subjects.map(
                                        x =>
                                            x.percentage
                                    ),

                                backgroundColor:
                                    "#19959a",

                                borderRadius:
                                    8,

                                borderSkipped:
                                    false,

                                maxBarThickness:
                                    48
                            }
                        ]
                    },

                    options: {
                        responsive: true,

                        maintainAspectRatio:
                            false,

                        plugins: {
                            legend: {
                                display:
                                    false
                            },

                            tooltip: {
                                callbacks: {
                                    title:
                                        function (
                                            items
                                        ) {
                                            const index =
                                                items[0]
                                                    .dataIndex;

                                            return data
                                                .subjects[
                                                index
                                            ]
                                                .fullName;
                                        },

                                    label:
                                        function (
                                            context
                                        ) {
                                            return (
                                                context
                                                    .raw +
                                                "%"
                                            );
                                        }
                                }
                            }
                        },

                        scales: {
                            y: {
                                beginAtZero:
                                    true,

                                max:
                                    100,

                                ticks: {
                                    callback:
                                        function (
                                            value
                                        ) {
                                            return (
                                                value +
                                                "%"
                                            );
                                        }
                                },

                                grid: {
                                    color:
                                        "#edf1f6"
                                },

                                border: {
                                    display:
                                        false
                                }
                            },

                            x: {
                                grid: {
                                    display:
                                        false
                                },

                                border: {
                                    display:
                                        false
                                }
                            }
                        }
                    }
                }
            );
        }



        // ==========================================
        // ATTENDANCE STATUS
        // ==========================================

        const statusCanvas =
            document.getElementById(
                "attendanceStatusChart"
            );


        if (
            statusCanvas &&
            data.status
        ) {

            new Chart(
                statusCanvas,
                {
                    type:
                        "doughnut",

                    data: {
                        labels: [
                            "Present",
                            "Absent",
                            "Leave"
                        ],

                        datasets: [
                            {
                                data: [
                                    data.status.present,
                                    data.status.absent,
                                    data.status.leave
                                ],

                                backgroundColor: [
                                    "#14827a",
                                    "#d4562a",
                                    "#2f69ea"
                                ],

                                borderWidth:
                                    0,

                                hoverOffset:
                                    4
                            }
                        ]
                    },

                    options: {
                        responsive:
                            true,

                        maintainAspectRatio:
                            false,

                        cutout:
                            "68%",

                        plugins: {
                            legend: {
                                display:
                                    false
                            }
                        }
                    }
                }
            );
        }



        // ==========================================
        // ABSENCE BY DAY
        // ==========================================

        const dayCanvas =
            document.getElementById(
                "absenceByDayChart"
            );


        if (
            dayCanvas &&
            data.days
        ) {

            new Chart(
                dayCanvas,
                {
                    type:
                        "bar",

                    data: {
                        labels:
                            data.days.map(
                                x =>
                                    x.label
                            ),

                        datasets: [
                            {
                                label:
                                    "Absent",

                                data:
                                    data.days.map(
                                        x =>
                                            x.value
                                    ),

                                backgroundColor:
                                    "#3974ea",

                                borderRadius:
                                    8,

                                borderSkipped:
                                    false,

                                maxBarThickness:
                                    62
                            }
                        ]
                    },

                    options: {
                        responsive:
                            true,

                        maintainAspectRatio:
                            false,

                        plugins: {
                            legend: {
                                display:
                                    false
                            }
                        },

                        scales: {
                            y: {
                                beginAtZero:
                                    true,

                                ticks: {
                                    precision:
                                        0
                                },

                                grid: {
                                    color:
                                        "#edf1f6"
                                },

                                border: {
                                    display:
                                        false
                                }
                            },

                            x: {
                                grid: {
                                    display:
                                        false
                                },

                                border: {
                                    display:
                                        false
                                }
                            }
                        }
                    }
                }
            );
        }

    }
);