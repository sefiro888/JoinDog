mergeInto(LibraryManager.library, {
  DogCrushVibrate: function (durationMs) {
    if (typeof navigator !== 'undefined' && navigator.vibrate) {
      navigator.vibrate(durationMs);
    }
  }
});
