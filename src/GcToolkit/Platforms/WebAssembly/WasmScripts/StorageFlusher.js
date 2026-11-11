// Copies the in-memory browser file system (IDBFS) to IndexedDB. Called after every committed database write.
globalThis.gcToolkitSyncFs = function () {
    return new Promise(function (resolve) {
        var fs = (globalThis.Module && globalThis.Module.FS) || globalThis.FS;
        if (!fs || !fs.syncfs) {
            console.warn("gcToolkitSyncFs: FS.syncfs is not available; data was not flushed.");
            resolve(false);
            return;
        }

        fs.syncfs(false, function (err) {
            if (err) {
                console.error("gcToolkitSyncFs failed", err);
            }

            resolve(!err);
        });
    });
};

// Last chance when the tab is hidden or closed; may not finish, which is why writes flush eagerly too.
document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "hidden") {
        globalThis.gcToolkitSyncFs();
    }
});
window.addEventListener("pagehide", function () {
    globalThis.gcToolkitSyncFs();
});
