window.addEventListener('load', function () {
    window.setTimeout(function () {
        var bubble = new google.bookmarkbubble.Bubble();

        var parameter = 'bmb=1';

        bubble.hasHashParameter = function () {
            return window.location.hash.indexOf(parameter) != -1;
        };

        bubble.setHashParameter = function () {
            if (!this.hasHashParameter()) {
                window.location.hash += parameter;
            }
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