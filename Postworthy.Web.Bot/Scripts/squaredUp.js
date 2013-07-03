/* 
Author: Landon Key
This script looks for data-squared attribute and makes the container squared based on the property set 
(i.e. - height, width).
*/

$(document).ready(function () {
    squaredUp = (function () {
        var squareUp = function (o) {
            var max = parseFloat(o.attr("data-squared-max") ? o.attr("data-squared-max") : "0");
            var percent = parseFloat(o.attr("data-squared-percent") ? o.attr("data-squared-percent") : "1.0");
            var changeAttr = o.attr("data-squared").toLowerCase();
            if (changeAttr == "width" || changeAttr == "height") {
                var matchAttr = changeAttr == "width" ? "height" : "width";
                var newContentSize = max > 0 && o[matchAttr]() * percent > max ? max : o[matchAttr]() * percent;
                o.css(changeAttr, newContentSize + "px");
                
                if (changeAttr == "height") {
                    o.children("[data-squared='content']")
                        .css("display", "block")
                        .css("margin-top", ((newContentSize - o.children("[data-squared='content']").height()) / 2) + "px");
                }
                
                setTimeout(function () {
                    o.children("[data-squared]").each(function (i, o2) {
                        o2 = $(o2);
                        squareUp(o2);
                    });
                }, 250);
            }
        };

        var init = function (context) {
            $("[data-squared]",context).each(function (i, o) {
                o = $(o);
                if (o.parents().filter("[data-squared]").length == 0) {
                    squareUp(o);
                }
            });
        };

        return {
            init: init,
            squareUp: function (o) { init($(o)) }
        };
    })();

    squaredUp.init();

    $(window).smartresize(squaredUp.init);
});