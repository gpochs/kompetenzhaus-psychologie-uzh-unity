mergeInto(LibraryManager.library, {
  KompetenzhausEmitState: function (pointer) {
    var json = UTF8ToString(pointer);
    if (window.KompetenzhausBridge && typeof window.KompetenzhausBridge.receive === 'function') {
      window.KompetenzhausBridge.receive(json);
    } else {
      window.__kompetenzhausPendingState = json;
    }
  }
});
