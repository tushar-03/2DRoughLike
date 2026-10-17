// JavaScript side of Roughlike.Utils.WebBridge (browser builds only).
mergeInto(LibraryManager.library, {

  // Android browsers: navigator.vibrate. Browsers without it (iOS Safari) simply do nothing here;
  // iOS gets feedback from Roughlike_SetTouchHaptics instead.
  Roughlike_Vibrate: function (milliseconds) {
    try {
      if (navigator.vibrate) navigator.vibrate(milliseconds);
    } catch (e) {}
  },

  Roughlike_IsTouchDevice: function () {
    try {
      var coarse = window.matchMedia && window.matchMedia('(pointer: coarse)').matches;
      var touch = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);
      var phoneUa = /Android|iPhone|iPad|iPod|Mobile|Tablet/i.test(navigator.userAgent || '');
      var ipadOnMac = (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
      return ((coarse && touch) || phoneUa || ipadOnMac) ? 1 : 0;
    } catch (e) { return 0; }
  },

  Roughlike_IsIOS: function () {
    try {
      var ua = navigator.userAgent || '';
      var ipadOnMac = (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
      return (/iPad|iPhone|iPod/.test(ua) || ipadOnMac) ? 1 : 0;
    } catch (e) { return 0; }
  },

  // iOS 17.4+ Safari plays a small haptic tick when a switch-style checkbox is toggled by a real touch.
  // The tick has to happen inside the touch event, so it is wired to touchstart rather than to game events.
  Roughlike_SetTouchHaptics: function (enabled) {
    try {
      if (!window.__rlHaptic) {
        var label = document.createElement('label');
        label.style.cssText = 'position:fixed;left:-200px;top:-200px;opacity:0;pointer-events:none;';
        var box = document.createElement('input');
        box.type = 'checkbox';
        box.setAttribute('switch', '');
        label.appendChild(box);
        document.body.appendChild(label);
        window.__rlHaptic = { label: label, on: false };
        window.addEventListener('touchstart', function () {
          if (window.__rlHaptic.on) window.__rlHaptic.label.click();
        }, { passive: true });
      }
      window.__rlHaptic.on = !!enabled;
    } catch (e) {}
  },

  // Persist the virtual file system (saves and settings live under /idbfs) to IndexedDB.
  Roughlike_SyncFS: function () {
    try {
      if (typeof FS !== 'undefined' && FS.syncfs) {
        FS.syncfs(false, function (err) { if (err) console.warn('Roughlike save sync failed', err); });
      }
    } catch (e) {}
  }
});
