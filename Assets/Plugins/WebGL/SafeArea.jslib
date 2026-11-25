mergeInto(LibraryManager.library, {
  // Writes the screen's safe-area insets (top, right, bottom, left) into four floats, in the canvas's backing pixels.
  // They come from the template's #safe-area-probe, whose padding is env(safe-area-inset-*); without it they are zero.
  SafeAreaReadInsets: function (insets) {
    var values = [0, 0, 0, 0];
    var probe = document.getElementById("safe-area-probe");
    var canvas = Module["canvas"];
    var box = canvas ? canvas.getBoundingClientRect() : null;
    if (probe && box && box.width > 0 && box.height > 0) {
      // The page sets its own devicePixelRatio for Unity, so the canvas itself says how many pixels a CSS pixel is.
      var style = getComputedStyle(probe);
      var sx = canvas.width / box.width, sy = canvas.height / box.height;
      values = [parseFloat(style.paddingTop) * sy, parseFloat(style.paddingRight) * sx,
                parseFloat(style.paddingBottom) * sy, parseFloat(style.paddingLeft) * sx];
    }
    for (var i = 0; i < 4; i++) HEAPF32[(insets >> 2) + i] = values[i] || 0;
  },
});
