mergeInto(LibraryManager.library, {
    PortCargoStorageRead: function(keyPointer) {
        var result;
        try {
            var value = wx.getStorageSync(UTF8ToString(keyPointer));
            if (value !== '' && typeof value !== 'string') throw new Error('Unexpected stored value type');
            result = {ok: true, present: value !== '', value: value};
        } catch (error) {
            result = {ok: false, error: String(error && error.message || error)};
        }
        var text = JSON.stringify(result);
        var buffer = _malloc(lengthBytesUTF8(text) + 1);
        stringToUTF8(text, buffer, lengthBytesUTF8(text) + 1);
        return buffer;
    },
    PortCargoStorageWrite: function(keyPointer, valuePointer) {
        var errorText = '';
        try {
            var key = UTF8ToString(keyPointer), value = UTF8ToString(valuePointer);
            wx.setStorageSync(key, value);
            if (wx.getStorageSync(key) !== value) throw new Error('Persistent readback mismatch');
        } catch (error) { errorText = String(error && error.message || error); }
        var buffer = _malloc(lengthBytesUTF8(errorText) + 1);
        stringToUTF8(errorText, buffer, lengthBytesUTF8(errorText) + 1);
        return buffer;
    }
});
