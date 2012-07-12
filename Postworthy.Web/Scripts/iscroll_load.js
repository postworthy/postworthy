var myScroll,
	pullDownEl, pullDownOffset,
	generatedCount = 0;

function pullDownAction() {
    $.get(
        "/mobile/refresh",
        function (data) {
            $("#scrollerItems").html(data);
            myScroll.refresh();
        }
    );
}

function iScrollLoaded() {
    if (myScroll) {
        myScroll.refresh();
    }
    else {
        pullDownEl = document.getElementById('pullDown');
        pullDownOffset = pullDownEl.offsetHeight;
        myScroll = new iScroll('wrapper', {
            snap: 'li',
            useTransition: true,
            topOffset: pullDownOffset,
            onRefresh: function () {
                if (pullDownEl.className.match('loading')) {
                    pullDownEl.className = '';
                    pullDownEl.querySelector('.pullDownLabel').innerHTML = 'Pull down to refresh...';
                }
            },
            onScrollMove: function () {
                if (this.y > 5 && !pullDownEl.className.match('flip')) {
                    pullDownEl.className = 'flip';
                    pullDownEl.querySelector('.pullDownLabel').innerHTML = 'Release to refresh...';
                    this.minScrollY = 0;
                } else if (this.y < 5 && pullDownEl.className.match('flip')) {
                    pullDownEl.className = '';
                    pullDownEl.querySelector('.pullDownLabel').innerHTML = 'Pull down to refresh...';
                    this.minScrollY = -pullDownOffset;
                }
            },
            onScrollEnd: function () {
                if (pullDownEl.className.match('flip')) {
                    pullDownEl.className = 'loading';
                    pullDownEl.querySelector('.pullDownLabel').innerHTML = 'Loading...';
                    pullDownAction(); // Execute custom function (ajax call?)
                }
            }

        });
    }
	setTimeout(function () { document.getElementById('wrapper').style.left = '0'; }, 800);
}