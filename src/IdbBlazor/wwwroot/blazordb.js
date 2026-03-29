// IdbBlazor - Slim IndexedDB Adapter for Blazor
// Provides a bridge between .NET/Blazor and the browser's IndexedDB API.
// This file is served as a static web asset from _content/IdbBlazor/blazordb.js

(function () {
    'use strict';

    /** @type {Object.<string, IDBDatabase>} */
    const _dbs = {};

    /** @type {Object.<number, number>} */
    const _subscriptions = {};

    let _subIdCounter = 0;

    /**
     * Gets an open database handle by name.
     * @param {string} dbName
     * @returns {IDBDatabase}
     */
    function getDb(dbName) {
        const db = _dbs[dbName];
        if (!db) throw new Error(`IdbBlazor: Database '${dbName}' is not open. Call openDatabase first.`);
        return db;
    }

    /**
     * Builds an IDBKeyRange from a plain-object descriptor.
     * @param {{ only?: any, lower?: any, upper?: any, lowerOpen?: boolean, upperOpen?: boolean }|null} q
     * @returns {IDBKeyRange|null}
     */
    function makeRange(q) {
        if (!q) return null;
        if (q.only !== undefined) return IDBKeyRange.only(q.only);
        const hasLower = q.lower !== undefined;
        const hasUpper = q.upper !== undefined;
        if (hasLower && hasUpper)
            return IDBKeyRange.bound(q.lower, q.upper, q.lowerOpen === true, q.upperOpen === true);
        if (hasLower)
            return IDBKeyRange.lowerBound(q.lower, q.lowerOpen === true);
        if (hasUpper)
            return IDBKeyRange.upperBound(q.upper, q.upperOpen === true);
        return null;
    }

    /**
     * Wraps an IDBRequest in a Promise.
     * @template T
     * @param {IDBRequest<T>} request
     * @returns {Promise<T>}
     */
    function promisifyRequest(request) {
        return new Promise((resolve, reject) => {
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    const IdbBlazor = {

        /**
         * Opens (and optionally upgrades) an IndexedDB database.
         * @param {string} dbName - The database name.
         * @param {number} version - The target database version.
         * @param {string} schemaJson - JSON-serialized IdbSchema containing version deltas.
         * @returns {Promise<{oldVersion: number, newVersion: number}>}
         */
        openDatabase: function (dbName, version, schemaJson) {
            return new Promise((resolve, reject) => {
                const schema = JSON.parse(schemaJson);
                let capturedOldVersion = 0;

                const request = indexedDB.open(dbName, version);

                request.onupgradeneeded = function (event) {
                    const db = event.target.result;
                    const tx = event.target.transaction;
                    capturedOldVersion = event.oldVersion;

                    for (const vDef of schema.versions || []) {
                        if (vDef.version <= capturedOldVersion) continue;

                        // Create new stores
                        for (const storeDef of vDef.createStores || []) {
                            const storeOptions = {};
                            if (storeDef.keyPath !== null && storeDef.keyPath !== undefined) {
                                storeOptions.keyPath = storeDef.keyPath;
                            }
                            storeOptions.autoIncrement = storeDef.autoIncrement === true;

                            const store = db.createObjectStore(storeDef.name, storeOptions);

                            for (const idx of storeDef.indexes || []) {
                                store.createIndex(idx.name, idx.keyPath, {
                                    unique: idx.unique === true,
                                    multiEntry: idx.multiEntry === true
                                });
                            }
                        }

                        // Modify existing stores (add/remove indexes)
                        for (const modDef of vDef.modifyStores || []) {
                            const store = tx.objectStore(modDef.name);
                            for (const idx of modDef.addIndexes || []) {
                                if (!store.indexNames.contains(idx.name)) {
                                    store.createIndex(idx.name, idx.keyPath, {
                                        unique: idx.unique === true,
                                        multiEntry: idx.multiEntry === true
                                    });
                                }
                            }
                            for (const idxName of modDef.deleteIndexes || []) {
                                if (store.indexNames.contains(idxName)) {
                                    store.deleteIndex(idxName);
                                }
                            }
                        }

                        // Delete stores
                        for (const storeName of vDef.deleteStores || []) {
                            if (db.objectStoreNames.contains(storeName)) {
                                db.deleteObjectStore(storeName);
                            }
                        }
                    }
                };

                request.onsuccess = function (event) {
                    _dbs[dbName] = event.target.result;
                    resolve({ oldVersion: capturedOldVersion, newVersion: version });
                };

                request.onerror = function (event) {
                    reject(event.target.error);
                };

                request.onblocked = function () {
                    reject(new Error(`IdbBlazor: Database '${dbName}' upgrade is blocked by another open connection.`));
                };
            });
        },

        /**
         * Adds a new record to an object store. Throws if a record with the same key already exists.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} valueJson - JSON-serialized record.
         * @returns {Promise<string>} - JSON-serialized resulting key.
         */
        add: function (dbName, storeName, valueJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const value = JSON.parse(valueJson);
                const tx = db.transaction([storeName], 'readwrite');
                const store = tx.objectStore(storeName);
                const req = store.add(value);
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Puts (adds or updates) a record in an object store.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} valueJson - JSON-serialized record.
         * @returns {Promise<string>} - JSON-serialized resulting key.
         */
        put: function (dbName, storeName, valueJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const value = JSON.parse(valueJson);
                const tx = db.transaction([storeName], 'readwrite');
                const store = tx.objectStore(storeName);
                const req = store.put(value);
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Gets a single record by its primary key.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} keyJson - JSON-serialized key.
         * @returns {Promise<string|null>} - JSON-serialized record, or null if not found.
         */
        get: function (dbName, storeName, keyJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const key = JSON.parse(keyJson);
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const req = store.get(key);
                req.onsuccess = () => resolve(req.result !== undefined ? JSON.stringify(req.result) : null);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Deletes a record by its primary key.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} keyJson - JSON-serialized key.
         * @returns {Promise<boolean>}
         */
        delete: function (dbName, storeName, keyJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const key = JSON.parse(keyJson);
                const tx = db.transaction([storeName], 'readwrite');
                const store = tx.objectStore(storeName);
                const req = store.delete(key);
                req.onsuccess = () => resolve(true);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Gets all records from an object store, optionally filtered by a key range.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string|null} rangeJson - JSON-serialized key range descriptor, or null for all records.
         * @returns {Promise<string>} - JSON-serialized array of records.
         */
        getAll: function (dbName, storeName, rangeJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const range = rangeJson ? makeRange(JSON.parse(rangeJson)) : null;
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const req = range ? store.getAll(range) : store.getAll();
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Gets all records matching an index key range.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} indexName
         * @param {string} rangeJson - JSON-serialized key range descriptor.
         * @returns {Promise<string>} - JSON-serialized array of records.
         */
        getAllByIndex: function (dbName, storeName, indexName, rangeJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const range = makeRange(JSON.parse(rangeJson));
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const index = store.index(indexName);
                const req = index.getAll(range);
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Gets all primary keys from an object store, optionally by key range.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string|null} rangeJson
         * @returns {Promise<string>} - JSON-serialized array of keys.
         */
        getAllKeys: function (dbName, storeName, rangeJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const range = rangeJson ? makeRange(JSON.parse(rangeJson)) : null;
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const req = range ? store.getAllKeys(range) : store.getAllKeys();
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Gets all primary keys matching an index key range.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} indexName
         * @param {string} rangeJson
         * @returns {Promise<string>} - JSON-serialized array of keys.
         */
        getAllKeysByIndex: function (dbName, storeName, indexName, rangeJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const range = makeRange(JSON.parse(rangeJson));
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const index = store.index(indexName);
                const req = index.getAllKeys(range);
                req.onsuccess = () => resolve(JSON.stringify(req.result));
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Counts records in an object store.
         * @param {string} dbName
         * @param {string} storeName
         * @returns {Promise<number>}
         */
        count: function (dbName, storeName) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const req = store.count();
                req.onsuccess = () => resolve(req.result);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Counts records matching an index key range.
         * @param {string} dbName
         * @param {string} storeName
         * @param {string} indexName
         * @param {string} rangeJson
         * @returns {Promise<number>}
         */
        countByIndex: function (dbName, storeName, indexName, rangeJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const range = makeRange(JSON.parse(rangeJson));
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const index = store.index(indexName);
                const req = index.count(range);
                req.onsuccess = () => resolve(req.result);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Clears all records from an object store.
         * @param {string} dbName
         * @param {string} storeName
         * @returns {Promise<boolean>}
         */
        clear: function (dbName, storeName) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const tx = db.transaction([storeName], 'readwrite');
                const store = tx.objectStore(storeName);
                const req = store.clear();
                req.onsuccess = () => resolve(true);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Executes a batch of operations within a single IndexedDB transaction.
         * Operations are executed sequentially; reads yield their result inline so
         * that the transaction remains active throughout.
         *
         * @param {string} dbName
         * @param {string} storeNamesJson - JSON array of store names to include in the transaction.
         * @param {string} mode - 'readonly' or 'readwrite'.
         * @param {string} operationsJson - JSON array of {type, store, value?, key?} objects.
         * @returns {Promise<string>} - JSON array of per-operation results (null for write ops).
         */
        executeTransaction: function (dbName, storeNamesJson, mode, operationsJson) {
            return new Promise((resolve, reject) => {
                const db = getDb(dbName);
                const storeNames = JSON.parse(storeNamesJson);
                const operations = JSON.parse(operationsJson);
                const results = new Array(operations.length).fill(null);
                const tx = db.transaction(storeNames, mode === 'readwrite' ? 'readwrite' : 'readonly');

                tx.onerror = () => reject(tx.error);
                tx.onabort = () => reject(new Error('IdbBlazor: Transaction aborted.'));

                function runOp(i) {
                    if (i >= operations.length) return;
                    const op = operations[i];
                    if (!op) { runOp(i + 1); return; }
                    const store = tx.objectStore(op.store);
                    let req;
                    switch (op.type) {
                        case 'add':  req = store.add(JSON.parse(op.value));   break;
                        case 'put':  req = store.put(JSON.parse(op.value));   break;
                        case 'delete': req = store.delete(JSON.parse(op.key)); break;
                        case 'get':  req = store.get(JSON.parse(op.key));    break;
                        case 'getAll': req = store.getAll();                  break;
                        default:
                            reject(new Error(`IdbBlazor: Unknown operation type '${op.type}'.`));
                            return;
                    }
                    req.onsuccess = () => {
                        if (req.result !== undefined) {
                            results[i] = JSON.stringify(req.result);
                        }
                        runOp(i + 1);
                    };
                    req.onerror = () => reject(req.error);
                }

                tx.oncomplete = () => resolve(JSON.stringify(results));
                runOp(0);
            });
        },

        /**
         * Subscribes to changes in an object store using poll-based observation.
         * The .NET callback is invoked with the serialized store contents whenever
         * they change.
         *
         * @param {string} dbName
         * @param {string} storeName
         * @param {any} dotnetRef - DotNetObjectReference to invoke.
         * @param {string} callbackMethod - Method name to invoke on dotnetRef.
         * @param {number} [intervalMs=500] - Polling interval in milliseconds.
         * @returns {number} Subscription ID used to cancel the subscription.
         */
        subscribeToStore: function (dbName, storeName, dotnetRef, callbackMethod, intervalMs) {
            const subId = ++_subIdCounter;
            let lastSnapshot = null;

            const poll = function () {
                const db = _dbs[dbName];
                if (!db) return;
                const tx = db.transaction([storeName], 'readonly');
                const store = tx.objectStore(storeName);
                const req = store.getAll();
                req.onsuccess = function () {
                    const snapshot = JSON.stringify(req.result);
                    if (snapshot !== lastSnapshot) {
                        lastSnapshot = snapshot;
                        dotnetRef.invokeMethodAsync(callbackMethod, snapshot).catch(function (err) {
                            console.warn('IdbBlazor LiveQuery callback error:', err);
                        });
                    }
                };
            };

            _subscriptions[subId] = setInterval(poll, intervalMs || 500);
            poll(); // immediate first run
            return subId;
        },

        /**
         * Cancels a previously created store subscription.
         * @param {number} subId - The subscription ID returned by subscribeToStore.
         */
        unsubscribeFromStore: function (subId) {
            const id = _subscriptions[subId];
            if (id !== undefined) {
                clearInterval(id);
                delete _subscriptions[subId];
            }
        },

        /**
         * Deletes an IndexedDB database entirely.
         * @param {string} dbName
         * @returns {Promise<boolean>}
         */
        deleteDatabase: function (dbName) {
            return new Promise((resolve, reject) => {
                if (_dbs[dbName]) {
                    _dbs[dbName].close();
                    delete _dbs[dbName];
                }
                const req = indexedDB.deleteDatabase(dbName);
                req.onsuccess = () => resolve(true);
                req.onerror = () => reject(req.error);
            });
        },

        /**
         * Returns true if the named database is currently open.
         * @param {string} dbName
         * @returns {boolean}
         */
        isDatabaseOpen: function (dbName) {
            return !!_dbs[dbName];
        }
    };

    window.IdbBlazor = IdbBlazor;

})();
