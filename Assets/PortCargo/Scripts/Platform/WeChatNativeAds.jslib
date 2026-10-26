mergeInto(LibraryManager.library, {
  $PortCargoNativeAdBridge: {
    next: 1,
    records: {},
    background: false,
    listening: false,
    supported: function () {
      return typeof wx !== 'undefined' && typeof wx.createCustomAd === 'function';
    },
    active: function (record) {
      return record && record.alive && !record.closed && !record.failed;
    },
    hideNative: function (record) {
      if (!record || !record.ad) return;
      record.programmaticHide = true;
      record.visible = false;
      try {
        var result = record.ad.hide();
        if (result && typeof result.catch === 'function') result.catch(function () {});
      } catch (_) {}
    },
    destroyNative: function (record) {
      // Hide again even after destroy: a previously pending show Promise may finish late.
      PortCargoNativeAdBridge.hideNative(record);
      if (!record || !record.ad || record.nativeDestroyed) return;
      record.nativeDestroyed = true;
      try { record.ad.destroy(); } catch (_) {}
    },
    fail: function (record) {
      if (!record || !record.alive || record.failed) return;
      record.failed = true;
      PortCargoNativeAdBridge.destroyNative(record);
    },
    fits: function (record, width, height) {
      return isFinite(width) && isFinite(height) && width > 0 && height > 0 &&
        width <= record.width + 0.5 && height <= record.height + 0.5;
    },
    measureStyle: function (record) {
      var bridge = PortCargoNativeAdBridge;
      if (!bridge.active(record) || !record.loaded || record.sizeFromEvent || !record.ad) return;
      // CustomAd does not promise these optional native fields. Absence is not zero size,
      // and style.width/height are requests, never proof of the rendered dimensions.
      try {
        var style = record.ad.style;
        var width = style && style.realWidth, height = style && style.realHeight;
        if (typeof width !== 'number' || typeof height !== 'number' ||
            !isFinite(width) || !isFinite(height) || width <= 0 || height <= 0) return;
        record.hasSize = true;
        record.actualWidth = width;
        record.actualHeight = height;
        if (!bridge.fits(record, width, height)) bridge.fail(record);
      } catch (_) {}
    },
    listen: function () {
      var bridge = PortCargoNativeAdBridge;
      if (bridge.listening) return;
      bridge.listening = true;
      if (typeof wx.onHide === 'function') wx.onHide(function () {
        bridge.background = true;
        Object.keys(bridge.records).forEach(function (key) { bridge.hideNative(bridge.records[key]); });
      });
      // C# must re-authorize a still-valid screen before any later Show call.
      if (typeof wx.onShow === 'function') wx.onShow(function () { bridge.background = false; });
    },
    attach: function (record, event, callback) {
      var method = record.ad['on' + event];
      if (typeof method !== 'function') return;
      record.listeners.push([event, callback]);
      method.call(record.ad, callback);
    },
    destroy: function (record) {
      if (!record) return;
      record.alive = false;
      delete PortCargoNativeAdBridge.records[record.id];
      if (record.ad) record.listeners.forEach(function (item) {
        try {
          var off = record.ad['off' + item[0]];
          if (typeof off === 'function') off.call(record.ad, item[1]);
        } catch (_) {}
      });
      record.listeners = [];
      PortCargoNativeAdBridge.destroyNative(record);
    }
  },

  PortCargoNativeAdSupported__deps: ['$PortCargoNativeAdBridge'],
  PortCargoNativeAdSupported: function () {
    return PortCargoNativeAdBridge.supported() ? 1 : 0;
  },

  PortCargoNativeAdCreate__deps: ['$PortCargoNativeAdBridge', '$UTF8ToString'],
  PortCargoNativeAdCreate: function (adUnitId, left, top, width, height, sizing, fixedWidth, fixedHeight) {
    var bridge = PortCargoNativeAdBridge;
    if (!bridge.supported() || bridge.background || !isFinite(left) || !isFinite(top) ||
        !isFinite(width) || !isFinite(height) || left < 0 || top < 0 || width <= 0 || height <= 0) return 0;
    if (sizing === 2 && (!isFinite(fixedWidth) || !isFinite(fixedHeight) ||
        fixedWidth <= 0 || fixedHeight <= 0 || width < fixedWidth || height < fixedHeight)) return 0;
    var record = null;
    try {
      if (typeof wx.getWindowInfo === 'function') {
        var info = wx.getWindowInfo();
        if (info && (left + width > info.windowWidth + 0.5 || top + height > info.windowHeight + 0.5)) return 0;
      }
      bridge.listen();
      record = {
        id: bridge.next++, alive: true, ad: null, listeners: [],
        left: left, top: top, width: width, height: height,
        actualWidth: 0, actualHeight: 0, hasSize: false,
        sizeFromEvent: false, horizontalFallback: sizing === 1,
        fixedWidth: sizing === 2 ? fixedWidth : 0, fixedHeight: sizing === 2 ? fixedHeight : 0,
        loaded: false, closed: false, failed: false, visible: false,
        showPending: false, programmaticHide: false, nativeDestroyed: false
      };
      bridge.records[record.id] = record;
      // Omit adIntervals: this adapter never requests periodic refreshes.
      record.ad = wx.createCustomAd({
        adUnitId: UTF8ToString(adUnitId),
        style: { left: left, top: top, width: width }
      });
      if (!record.ad) throw new Error('custom-ad-unavailable');
      bridge.attach(record, 'Load', function () {
        if (bridge.active(record)) { record.loaded = true; bridge.measureStyle(record); }
      });
      bridge.attach(record, 'Error', function () { bridge.fail(record); });
      bridge.attach(record, 'Close', function () {
        if (!record.alive || record.closed) return;
        record.closed = true;
        bridge.destroyNative(record);
      });
      bridge.attach(record, 'Hide', function () {
        if (!record.alive || record.programmaticHide) return;
        // Some templates report a user dismissal through onHide instead of onClose.
        record.closed = true;
        bridge.destroyNative(record);
      });
      bridge.attach(record, 'Resize', function (size) {
        if (!bridge.active(record)) return;
        record.hasSize = true;
        record.sizeFromEvent = true;
        record.actualWidth = Number(size && size.width);
        record.actualHeight = Number(size && size.height);
        // Guard at the native event boundary, even while Unity is paused or busy.
        if (!bridge.fits(record, record.actualWidth, record.actualHeight)) bridge.fail(record);
      });
      return record.id;
    } catch (_) {
      if (record) bridge.destroy(record);
      return 0;
    }
  },

  PortCargoNativeAdRead__deps: ['$PortCargoNativeAdBridge'],
  PortCargoNativeAdRead: function (handle, key) {
    var record = PortCargoNativeAdBridge.records[handle];
    if (!record) return key === 0 ? 4 : 0;
    if (key === 0) PortCargoNativeAdBridge.measureStyle(record);
    if (key === 1) return record.actualWidth;
    if (key === 2) return record.actualHeight;
    return (record.loaded ? 1 : 0) | (record.closed ? 2 : 0) | (record.failed ? 4 : 0) |
      (record.visible ? 8 : 0) | (record.hasSize ? 16 : 0);
  },

  PortCargoNativeAdShow__deps: ['$PortCargoNativeAdBridge'],
  PortCargoNativeAdShow: function (handle) {
    var bridge = PortCargoNativeAdBridge;
    var record = bridge.records[handle];
    if (!bridge.active(record) || !record.loaded || record.showPending || record.visible) return;
    if (bridge.background) { bridge.hideNative(record); return; }
    bridge.measureStyle(record);
    if (!bridge.active(record)) return;
    if (!record.hasSize && !record.horizontalFallback && !record.fixedWidth) return;
    var width = record.hasSize ? record.actualWidth : record.fixedWidth || record.width;
    var height = record.hasSize ? record.actualHeight : record.fixedHeight || Math.ceil(record.width * 7 / 20);
    if (!bridge.fits(record, width, height)) { bridge.fail(record); return; }
    record.programmaticHide = false;
    record.showPending = true;
    try {
      Promise.resolve(record.ad.show()).then(function () {
        record.showPending = false;
        if (!bridge.active(record) || bridge.background || record.programmaticHide) {
          bridge.destroyNative(record);
          return;
        }
        bridge.measureStyle(record);
        if (!bridge.active(record)) return;
        record.visible = true;
      }, function () {
        record.showPending = false;
        if (record.alive) bridge.fail(record);
        else bridge.destroyNative(record);
      });
    } catch (_) { record.showPending = false; bridge.fail(record); }
  },

  PortCargoNativeAdHide__deps: ['$PortCargoNativeAdBridge'],
  PortCargoNativeAdHide: function (handle) {
    PortCargoNativeAdBridge.hideNative(PortCargoNativeAdBridge.records[handle]);
  },

  PortCargoNativeAdDestroy__deps: ['$PortCargoNativeAdBridge'],
  PortCargoNativeAdDestroy: function (handle) {
    PortCargoNativeAdBridge.destroy(PortCargoNativeAdBridge.records[handle]);
  }
});
