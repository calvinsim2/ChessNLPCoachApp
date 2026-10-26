
(function () {
    "use strict";

    // -----------------------------------------
    // 1. EXISTING FILE VALIDATION SETTINGS
    // -----------------------------------------

    const MAX_FILE_SIZE = 64 * 1024;
    const ALLOWED_EXTENSION = ".pgn";
    const LOGIN_URL = "/Account/Login";

    const form = document.getElementById("pgnUploadForm");
    const fileInput = document.getElementById("pgnFile");
    const errorElement = document.getElementById("pgnFileError");
    const submitButton = document.getElementById("pgnUploadButton");

    const $uploadArea = $("#analysisUploadArea");

    // Store the progress panel while analysis is running.
    let $progressPanel = null;

    // -----------------------------------------
    // 2. EXISTING VALIDATION FUNCTIONS
    // -----------------------------------------

    function showError(message) {
        errorElement.textContent = message;
        fileInput.classList.add("is-invalid");
        fileInput.classList.remove("is-valid");
    }

    function clearError() {
        errorElement.textContent = "";
        fileInput.classList.remove("is-invalid");
        fileInput.classList.add("is-valid");
    }

    function validateFile() {
        const file = fileInput.files[0];

        if (!file) {
            return "Please select a file to upload.";
        }

        if (!file.name.toLowerCase().endsWith(ALLOWED_EXTENSION)) {
            return "Invalid file type. Only .pgn files are allowed.";
        }

        if (file.size === 0) {
            return "The selected file is empty.";
        }

        if (file.size > MAX_FILE_SIZE) {
            return "The selected file exceeds the 64 KB size limit.";
        }

        return null;
    }

    // -----------------------------------------
    // 3. SHOW AND UPDATE THE PROGRESS PANEL
    // -----------------------------------------

    function showProgressPanel() {

        // Create the card from the template in Index.cshtml.
        $progressPanel = $(
            $("#analysisProgressTemplate").html().trim()
        );

        // Block only the upload area.
        $uploadArea.block({
            message: $progressPanel,

            css: {
                border: "none",
                padding: 0,
                backgroundColor: "transparent",
                width: "90%",
                maxWidth: "460px"
            },

            overlayCSS: {
                backgroundColor: "#152238",
                opacity: 0.48,
                cursor: "wait"
            },

            centerX: true,
            centerY: true,
            baseZ: 1000
        });
    }

    function updateProgress(progress) {

        if (!$progressPanel) {
            return;
        }

        const total = Number(progress.totalPlys) || 0;

        const completed = Math.max(
            0,
            Math.min(
                total,
                Number(progress.completedPlys) || 0
            )
        );

        const percent = total > 0
            ? Math.round((completed / total) * 100)
            : 0;

        // Update the processed-ply counter.
        $progressPanel.find("[data-completed]")
            .text(completed);

        $progressPanel.find("[data-total]")
            .text(total);

        // Update the displayed percentage.
        $progressPanel.find("[data-percent]")
            .text(percent + "%");

        // Update the Bootstrap progress bar.
        $progressPanel.find("[data-progress-bar]")
            .css("width", percent + "%");

        $progressPanel.find(".progress")
            .attr("aria-valuenow", percent);

        // Update the latest move description.
        let message = "Uploading and validating PGN...";

        if (total > 0) {
            message = "Preparing chess analysis...";
        }

        if (progress.san) {
            message = "Last completed move: " + progress.san;
        }

        $progressPanel.find("[data-last-move]")
            .text(message);
    }

    // Fallback messages for responses that are not the
    // { toastType, toastMessage } JSON returned by the server.
    function describeHttpFailure(response) {

        switch (response.status) {
            case 400:
                return { type: "warn", text: "Your form has expired. Please refresh the page and try again." };
            case 401:
                return { type: "warn", text: "Your session has expired. Please sign in again.", loginUrl: LOGIN_URL };
            case 403:
                return { type: "error", text: "You are not allowed to perform this action." };
            case 413:
                return { type: "error", text: "The PGN file is too large." };
            case 429:
                return { type: "warn", text: "Too many uploads. Please try again later." };
            case 502:
            case 503:
            case 504:
                return { type: "error", text: "The server took too long to respond. Please try again later." };
            default:
                return { type: "error", text: "Unexpected server response (HTTP " + response.status + ")." };
        }
    }

    // -----------------------------------------
    // 4. EXISTING FILE INPUT VALIDATION
    // -----------------------------------------

    fileInput.addEventListener("change", function () {

        const error = validateFile();

        if (error) {
            showError(error);
            fileInput.value = "";
        }
        else {
            clearError();
        }
    });

    // -----------------------------------------
    // 5. HANDLE FORM SUBMISSION
    // -----------------------------------------

    form.addEventListener("submit", async function (event) {

        // Prevent normal form submission/navigation.
        event.preventDefault();

        // Prevent duplicate submissions.
        if (submitButton.disabled) {
            return;
        }

        const error = validateFile();

        if (error) {
            showError(error);
            return;
        }

        clearError();

        submitButton.disabled = true;
        submitButton.textContent = "Analysing...";

        // Create a unique identifier for this upload.
        const analysisId = crypto.randomUUID();

        showProgressPanel();

        updateProgress({
            completedPlys: 0,
            totalPlys: 0,
            san: ""
        });

        let connection = null;
        let navigating = false;
        let failure = null;

        try {

            // -----------------------------------------
            // 6. CONNECT TO SIGNALR
            // -----------------------------------------

            connection = new signalR.HubConnectionBuilder()
                .withUrl(form.dataset.progressHubUrl)
                .withAutomaticReconnect()
                .build();

            // Receive each completed-ply update.
            connection.on("AnalysisProgress", function (progress) {
                updateProgress(progress);
            });

            // Display connection-interruption feedback.
            connection.onreconnecting(function () {
                if ($progressPanel) {
                    $progressPanel.find("[data-last-move]")
                        .text("Progress connection interrupted. Reconnecting...");
                }
            });

            // Rejoin the group after reconnecting.
            connection.onreconnected(async function () {
                try {
                    await connection.invoke(
                        "JoinAnalysis",
                        analysisId
                    );
                }
                catch (rejoinError) {
                    console.error(rejoinError);
                }
            });

            try {
                // Establish the SignalR connection first.
                await connection.start();

                // Subscribe this browser to its analysis group.
                await connection.invoke(
                    "JoinAnalysis",
                    analysisId
                );
            }
            catch (hubError) {

                // SignalR wraps negotiate failures, so the status is only in the message.
                const hubMessage = String((hubError && hubError.message) || "");

                if (hubMessage.includes("Status code '401'")) {
                    const authError = new Error(
                        "Your session has expired. Please sign in again."
                    );

                    authError.toastType = "warn";
                    authError.loginUrl = LOGIN_URL;

                    throw authError;
                }

                // Live progress is optional; continue the upload without it.
                console.warn("Live progress unavailable.", hubError);

                if ($progressPanel) {
                    $progressPanel.find("[data-last-move]")
                        .text("Live progress unavailable. Analysing...");
                }
            }

            // -----------------------------------------
            // 7. UPLOAD PGN THROUGH AJAX
            // -----------------------------------------

            // Includes the file and antiforgery token.
            const formData = new FormData(form);

            // Add the SignalR analysis identifier.
            formData.append("analysisId", analysisId);

            const response = await fetch(form.action, {
                method: "POST",

                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "application/json"
                },

                body: formData,
                credentials: "same-origin"
            });

            // -----------------------------------------
            // 8. READ THE CONTROLLER RESPONSE
            // -----------------------------------------

            const contentType =
                response.headers.get("content-type") || "";

            // The AJAX controller should return JSON.
            // Anything else (e.g. a proxy error page) is mapped by status code.
            if (!contentType.includes("json")) {
                const info = describeHttpFailure(response);

                const httpError = new Error(info.text);

                httpError.toastType = info.type;
                httpError.loginUrl = info.loginUrl;

                throw httpError;
            }

            const data = await response.json();

            // Display errors returned by UploadFailure().
            if (!response.ok) {
                const uploadError = new Error(
                    data.toastMessage ||
                    data.message ||
                    "Upload failed (HTTP " + response.status + ")."
                );

                uploadError.toastType = data.toastType || "error";
                uploadError.loginUrl = data.loginUrl;

                throw uploadError;
            }

            // The controller must return a Results URL.
            if (!data.redirectUrl) {
                throw new Error(
                    "Analysis completed, but the Results URL is missing."
                );
            }

            // -----------------------------------------
            // 9. NAVIGATE TO COMPLETED RESULTS
            // -----------------------------------------

            window.location.assign(data.redirectUrl);
            navigating = true;
        }
        catch (error) {
            failure = error;
        }
        finally {

            // On failure, release the UI and SignalR connection.
            if (!navigating) {

                if (connection) {
                    await connection.stop().catch(console.error);
                }

                $uploadArea.unblock();

                submitButton.disabled = false;
                submitButton.textContent = "Upload";

                $progressPanel = null;
            }
        }

        // Display toastr after the overlay has been removed.
        if (failure) {

            const allowedTypes = [
                "success", "info", "warn", "error"
            ];

            const toastType = allowedTypes.includes(
                failure.toastType
            )
                ? failure.toastType
                : "error";

            uiCommon.notify[toastType](
                failure.message || "Unable to analyse the PGN."
            );

            // An expired session cannot continue, so send the user to sign in.
            if (failure.loginUrl) {
                setTimeout(function () {
                    window.location.assign(failure.loginUrl);
                }, 2000);
            }
        }
    });

})();
