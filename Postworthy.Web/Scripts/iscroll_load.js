var myScroll,
	pullDownEl, pullDownOffset,
	generatedCount = 0;

function pullDownAction(page) {
    $.mobile.changePage(page, { reloadPage: true });
}

function iScrollLoaded(page) {
    if (myScroll) {
        setTimeout(function () {
            myScroll.destroy();
            myScroll = null;
            iScrollLoaded(page);
        }, 200);
    }
    else {
        pullDownEl = document.getElementById('pullDown');
        pullDownOffset = pullDownEl.offsetHeight;
        myScroll = new iScroll($.mobile.activePage.find('#wrapper')[0].id, {
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
                    pullDownAction(page); // Execute custom function (ajax call?)
                }
            }

        });
    }
	setTimeout(function () { document.getElementById('wrapper').style.left = '0'; }, 800);
}