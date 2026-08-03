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

// Called synchronously from the "Starte App" click so the browser ties audio-autoplay
// permission to that click itself, instead of the later (async, IndexedDB-delayed) first
// play() call on the real syllable <audio> element - which otherwise happens too far removed
// from the gesture for some browsers to still allow it.
window.k4l_unlockAudio = function () {
    var el = document.createElement("audio");
    el.muted = true;
    var playPromise = el.play();
    if (playPromise) playPromise.catch(function () {}).finally(function () { el.pause(); });
};