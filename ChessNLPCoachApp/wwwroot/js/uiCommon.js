// simple namespacing technique in plain JavaScript.

// var uiCommon = uiCommon || {} checks “does a global uiCommon object already exist?”
// If yes, reuse it.
// If no, create a new empty object { }.

var uiCommon = uiCommon || {};
uiCommon.ui = uiCommon.ui || {};
uiCommon.notify = uiCommon.notify || {};
uiCommon.configs = uiCommon.configs || {};
uiCommon.utils = uiCommon.utils || {};
uiCommon.message = uiCommon.message || {};

(function () {

    if (typeof toastr === 'undefined') {
        return;
    }

    toastr.options = {
        "closeButton": false,
        "debug": false,
        "newestOnTop": false,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "preventDuplicates": true,
        "onclick": null,
        "showDuration": "300",
        "hideDuration": "1000",
        "timeOut": "5000",
        "extendedTimeOut": "1000",
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    }

    /* NOTIFICATION ***************/

    var showNotification = function (type, message, title, options) {
        // type is a string: one of "success", "error", "info", or "warning".
        // toastr[type] is equivalent to calling toastr.success(...), toastr.error(...), etc.
        toastr[type](message, title, options);
    };

    uiCommon.notify.success = function (message, title, options) {
        showNotification('success', message, title, options);
    };

    uiCommon.notify.info = function (message, title, options) {
        showNotification('info', message, title, options);
    };

    uiCommon.notify.warn = function (message, title, options) {
        showNotification('warning', message, title, options);
    };

    uiCommon.notify.error = function (message, title, options) {
        showNotification('error', message, title, options);
    };

    console.log(uiCommon);

})();