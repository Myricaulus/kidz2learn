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
// play() call - which otherwise happens too far removed from the gesture for some browsers
// to still allow it. Primes the *real*, persistently-mounted #audioPlayer element (see
// MainLayout.razor) rather than a detached dummy, so the gesture-linkage is tied to the
// same element that will actually play syllable audio afterward.
window.k4l_primeAudioPlayer = function (id) {
    var el = document.getElementById(id);
    if (!el) return;
    var playPromise = el.play();
    if (playPromise) playPromise.catch(function () {}).finally(function () {
        // By the time this settles, k4l_playAudioFile may already have loaded and started the
        // real audio (currentSrc is only non-empty once a real source has loaded) - don't pause
        // that just because the priming call is done.
        if (!el.currentSrc) el.pause();
    });
};

// Sets the src on the shared #audioPlayer element and plays it. Used by challenge pages
// instead of a Blazor src="@_currentAudio" binding, since the element itself now lives in
// MainLayout.razor, not in the challenge page's own markup.
window.k4l_playAudioFile = function (id, src) {
    var el = document.getElementById(id);
    if (!el) return;
    el.src = src;
    el.play();
};