(function () {
    var urlPattern = /(https?:\/\/[^\s"<]+)/g;

    function linkifyResponseBodies() {
        var blocks = document.querySelectorAll('.response_body pre, .response-body pre, .body-textarea, .curl pre, .response pre');
        for (var i = 0; i < blocks.length; i++) {
            var el = blocks[i];
            if (el.getAttribute('data-linkified')) {
                continue;
            }

            var text = el.textContent || el.innerText || '';
            if (!urlPattern.test(text)) {
                continue;
            }

            urlPattern.lastIndex = 0;
            el.innerHTML = text.replace(urlPattern, '<a href="$1" target="_blank" style="color:#4990e2;text-decoration:underline">$1</a>');
            el.setAttribute('data-linkified', 'true');
        }
    }

    setInterval(linkifyResponseBodies, 1500);
})();
