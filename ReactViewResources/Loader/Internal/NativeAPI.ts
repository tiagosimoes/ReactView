﻿import { nativeAPIObjectName } from "./Environment";

interface INativeObject {
    notifyViewInitialized(frameName: string): void;
    notifyViewLoaded(frameName: string, id: string): void;
    notifyViewDestroyed(frameName: string): void;
}

declare const cefglue: {
    checkObjectBound(objName: string): Promise<boolean>
};

function withAPI(action: (api: INativeObject) => void): void {
    const api = window[nativeAPIObjectName];
    if (api) {
        action(api);
    } else {
        bindNativeObject(nativeAPIObjectName).then(action);
    }
}

export async function bindNativeObject<T>(nativeObjectName: string): Promise<T> {
    if (typeof cefglue !== "undefined") {
        await cefglue.checkObjectBound(nativeObjectName);
    } else {
        await getRegisteredObject(nativeObjectName);
    }
    return window[nativeObjectName] as T;
}

var hasWaited = false;

async function getRegisteredObject(nativeObjectName) {
    while (window[nativeObjectName] == undefined) {
        await sleep(100); //TODO TCS: Not really sure why we need to wait for this to start the first time
        hasWaited = true;
    }
    return window[nativeObjectName];
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export function notifyViewInitialized(viewName: string): void {
    withAPI(api => api.notifyViewInitialized(viewName));
}

export function notifyViewLoaded(viewName: string, id: string): void {
    withAPI(api => api.notifyViewLoaded(viewName, id));
}

export function notifyViewDestroyed(viewName: string): void {
    withAPI(api => api.notifyViewDestroyed(viewName));
}