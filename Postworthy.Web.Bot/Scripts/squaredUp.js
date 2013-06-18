/* 
Author: Landon Key
This script looks for data-squared attribute and makes the container squared based on the property set 
(i.e. - height, width).
*/

$(document).ready(function () {
    $("[data-squared]").each(function (i, o) {
        o = $(o);
        var max = parseFloat(o.attr("data-squared-max") ? o.attr("data-squared-max") : "0");
        var changeAttr = o.attr("data-squared").toLowerCase();
        if (changeAttr == "width" || changeAttr == "height") {
            var matchAttr = changeAttr == "width" ? "height" : "width";
            var oldContentSize = o[changeAttr]();
            var newContentSize = max > 0 && o[matchAttr]() > max ? max : o[matchAttr]();
            o.css(changeAttr, newContentSize + "px");

            if (changeAttr == "height") {
                if (newContentSize - oldContentSize > 0)
                    o.css("padding-top", (newContentSize - oldContentSize) / 2);
            }
        }
    });
});