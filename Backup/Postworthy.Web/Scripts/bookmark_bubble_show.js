window.addEventListener('load', function () {
    window.setTimeout(function () {
        var bubble = new google.bookmarkbubble.Bubble();

        var BUBBLE_STORAGE_KEY = 'bubble';

        bubble.hasHashParameter = function () {
            return window.localStorage[BUBBLE_STORAGE_KEY];
        };

        bubble.setHashParameter = function () {
            window.localStorage[BUBBLE_STORAGE_KEY] = '1';
        };

        bubble.getViewportHeight = function () {
            return window.innerHeight;
        };

        bubble.getViewportScrollY = function () {
            return window.pageYOffset;
        };

        bubble.registerScrollHandler = function (handler) {
            window.addEventListener('scroll', handler, false);
        };

        bubble.deregisterScrollHandler = function (handler) {
            window.removeEventListener('scroll', handler, false);
        };

        bubble.showIfAllowed();
    }, 1000);
}, false);