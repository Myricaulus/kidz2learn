window.elementInterop = {
    blurElementById: function (id) {
        document.getElementById(id)?.blur();
    },
    emptyElementById: function (prefix, count) {
        for (let i = 0; i < count; i++) {
            let el = document.getElementById(prefix + i);
            if (el) {
                el.value = ""
            }
        }
    }
};