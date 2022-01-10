using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using static ReactViewControl.WebServer.SerializedObject;

namespace ReactViewControl.WebServer {
    public class ServerView {

        delegate object CallTargetMethod(Func<object> target);
        public static Action<ServerView, string> NewNativeObject { get; set; }
        public static Action<string, string> SendMessage { get; set; }


        private ReactViewRender.NativeAPI nativeAPI;
        readonly Dictionary<string, object> registeredObjects = new Dictionary<string, object>();
        readonly Dictionary<string, CallTargetMethod> registeredObjectInterceptMethods = new Dictionary<string, CallTargetMethod>();
        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);
        
        enum Operation {
            RegisterObjectName,
            UnregisterObjectName,
            EvaluateScriptFunctionWithSerializedParams,
            Execute,
            ResizePopup,
            ReturnValue,
            OpenURL,
            OpenURLInNewTab,
            OpenURLInPopup,
            OpenContextMenu,
            SetBrowserURL,
            CloseWindow,
            MenuClicked
        }

        public bool RegisterWebJavaScriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall, bool executeCallsInUI = false) {
            if (registeredObjects.ContainsKey(name)) {
                return false;
            }

            if (executeCallsInUI) {
                return RegisterWebJavaScriptObject(name, objectToBind, target => ExecuteInUI<object>(target), false);
            }

            if (interceptCall == null) {
                interceptCall = target => target();
            }

            object CallTargetMethod(Func<object> target) {
                // TODO TCS: Check if this is needded
                //if (isDisposing) {
                //    return null;
                //}
                try {
                    JavascriptPendingCalls.AddCount();
                    //if (isDisposing) {
                    //    // check again, to avoid concurrency problems with dispose
                    //    return null;
                    //}
                    return interceptCall(target);
                } finally {
                    JavascriptPendingCalls.Signal();
                }
            }

            registeredObjects[name] = objectToBind;
            registeredObjectInterceptMethods[name] = CallTargetMethod;
            if (registeredObjects.Count == 1) {
                nativeAPI = (ReactViewRender.NativeAPI)objectToBind;
                NewNativeObject(this, name);
            }
            SendWebSocketMessageRegister(name, objectToBind);
            return true;
        }

        public ReactView Host {
            get {
                while (nativeAPI.ViewRender.Host == null) {
                    Task.Delay(10);
                }
                return nativeAPI.ViewRender.Host;
            }
        }

        public void UnregisterWebJavaScriptObject(string name) {
            if (registeredObjects.ContainsKey(name)) {
                SendWebSocketMessage(Operation.UnregisterObjectName, name);
                registeredObjects.Remove(name);
            }
        }

        public void ExecuteWebScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            functionName = functionName.Replace("embedded://webview/", "/");
            SendWebSocketMessage(Operation.Execute, functionName, JsonSerializer.Serialize(args));
        }
        private void ReturnValue(float callKey, object value) {
            SendWebSocketMessage(Operation.ReturnValue, callKey.ToString(), JsonSerializer.Serialize(value, new JsonSerializerOptions() { MaxDepth = 512 }));
        }

        private readonly Dictionary<string, JsonElement> evaluateResults = new Dictionary<string, JsonElement>();

        internal Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string method, object[] args) {
            var evaluateKey = Guid.NewGuid().ToString();
            SendWebSocketMessageEvaluate(method, evaluateKey, args);
            var startTime = DateTime.Now;
            while (!evaluateResults.ContainsKey(evaluateKey)) {
                Task.Delay(50);
                if (DateTime.Now.Subtract(startTime).TotalSeconds > 30) {
                    throw new Exception(method + " took more than 30s");
                }
            }
            return Task.FromResult(JsonSerializer.Deserialize<T>((evaluateResults[evaluateKey]).GetRawText()));
        }

        internal void OpenContextMenu(ContextMenu menu) {
            IEnumerable<object> menuItems = GetMenuItems(menu.Items);
            SendWebSocketMessage(Operation.OpenContextMenu, JsonSerializer.Serialize(menuItems, new JsonSerializerOptions() { IncludeFields = true }));
        }

        private static IEnumerable<object> GetMenuItems(System.Collections.IEnumerable items) {
            return items.OfType<object>().Where(item => (item as MenuItem)?.IsVisible ?? true).Select(item =>
                    (item is MenuItem menuItem) ? (object)new WebMenuItem(menuItem) : new WebMenuSeparator()
            );
        }

        class WebMenuItem {
            public string Header;
            public bool IsEnabled;
            public bool IsVisible;
            public Image Icon;
            public bool IsBold;
            public int HashCode;
            public IEnumerable<object> Items;

            public WebMenuItem(MenuItem menuItem) {
                Header = (string)menuItem.Header;
                IsEnabled = menuItem.IsEnabled;
                IsVisible = menuItem.IsVisible;
                HashCode = menuItem.GetHashCode();
                //Icon = (Image)menuItem.Icon;
                IsBold = menuItem.FontWeight == Avalonia.Media.FontWeight.Bold;
                Items = GetMenuItems(menuItem.Items);
            }
        }

        struct WebMenuSeparator { }

        private T ExecuteInUI<T>(Func<T> action) {
            return Dispatcher.UIThread.CheckAccess() ? action() : Dispatcher.UIThread.InvokeAsync(action).Result;
        }

        public Stream GetCustomResource(string path, out string extension) {
            return nativeAPI.ViewRender.GetCustomResource(path, out extension);
        }

        public void OpenURLInPopup(string url) {
            SendWebSocketMessage(Operation.OpenURLInPopup, url);
        }

        public void OpenURL(string url) {
            SendWebSocketMessage(Operation.OpenURL, url);
        }

        public void OpenURLInNewTab(string url, bool inPopup = false) {
            SendWebSocketMessage(Operation.OpenURLInNewTab, url);
        }

        public void SetBrowserURL(string url) {
            SendWebSocketMessage(Operation.SetBrowserURL, url);
        }

        private void SetPopupDimensions() {
            var windowSettings = ExecuteInUI(() => {
                var window = (Window)nativeAPI.ViewRender.Host.Parent;
                return new WindowSettings() {
                    Height = double.IsNaN(window.Height)? 2000: window.Height,
                    Width = double.IsNaN(window.Width) ? 2000: window.Width,
                    Title = window.Title,
                    IsResizable = window.CanResize
                };
            });

            SendWebSocketMessage(Operation.ResizePopup, JsonSerializer.Serialize(windowSettings, new JsonSerializerOptions() { IncludeFields = true }));
        }

        public void ReceiveMessage(string text) {
            if (text.StartsWith("{\"EvaluateKey\"")) {
                var evaluateResult = DeserializeEvaluateResult(text);
                evaluateResults[evaluateResult.EvaluateKey] = evaluateResult.EvaluatedResult;
            } else if (text.StartsWith($"{{\"{Operation.CloseWindow}\"")) {
                CloseWindow();
            } else if (text.StartsWith($"{{\"{Operation.MenuClicked}\"")) {
                var menuClicked = DeserializeMenuClicked(text);
                ClickOnMenuItem(menuClicked);
            } else {
                var methodCall = DeserializeMethodCall(text);
                ExecuteMethod(methodCall);
            }
        }

        private void ExecuteMethod(MethodCall methodCall) {
            var obj = registeredObjects[methodCall.ObjectName];
            var callTargetMethod = registeredObjectInterceptMethods[methodCall.ObjectName];
            callTargetMethod(() => {
                var result = ExecuteMethod(obj, methodCall);
                if (obj.GetType().GetMethod(methodCall.MethodName).ReturnType != typeof(void)) {
                    ReturnValue(methodCall.CallKey, result);
                }
                if (methodCall.MethodName == "NotifyViewLoaded" && nativeAPI.ViewRender.Host.Parent is Window) {
                    // dialogs are opened in iframes and need to be redimensioned
                    if ((methodCall.Args is JsonElement args && args.EnumerateArray().First().GetString() != "") || 
                            nativeAPI.ViewRender.Host.GetType().ToString().EndsWith("ReactViewHostForPlugins")) {
                        Dispatcher.UIThread.InvokeAsync(() => SetPopupDimensions());
                    }
                }
                return result;
            });
        }

        internal static object ExecuteMethod(object obj, MethodCall methodCall) {
            var method = obj.GetType().GetMethod(methodCall.MethodName);
            List<object> arguments = new List<object>();
            if (methodCall.Args is JsonElement elem) {
                if (method.GetParameters().Length > 0) {
                    foreach (var item in elem.EnumerateArray().Select((value, index) => new { index, value })) {
                        var parameter = method.GetParameters()[item.index];
                        if (item.value.ValueKind == JsonValueKind.Array && !parameter.ParameterType.IsArray) {
                            foreach (var subitem in item.value.EnumerateArray().Select((value, index) => new { index, value })) {
                                var subparameter = method.GetParameters()[subitem.index];
                                arguments.Add(GetJSONValue(subitem.value, subparameter.ParameterType));
                            }
                            break;
                        } else {
                            arguments.Add(GetJSONValue(item.value, parameter.ParameterType));
                        }
                    }
                }
            }
            if (method.ReturnType == typeof(void)) {
                AsyncExecuteIfNeeded(() => 
                    obj.GetType().GetMethod(methodCall.MethodName).Invoke(obj, arguments.ToArray())
                );
                return null;
            } else {
                return obj.GetType().GetMethod(methodCall.MethodName).Invoke(obj, arguments.ToArray());
            }
        }

        private static object GetJSONValue(JsonElement elem, Type type) {
            switch (elem.ValueKind) {
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.True:
                    return true;
                default:
                    return JsonSerializer.Deserialize(elem.GetRawText(), type);
            };
            throw new NotImplementedException();
        }


        private static void AsyncExecuteIfNeeded(Action action) {
            if (Dispatcher.UIThread.CheckAccess()) {
                action();
            } else {
                Task.Run(action);
            }
        }


        private void CloseWindow() {
            Dispatcher.UIThread.InvokeAsync(() => {
                var window = nativeAPI.ViewRender.Host.Parent as Window;
                window.Close();
            });
        }

        private void ClickOnMenuItem(MenuClickedObject menuClicked) {
            Dispatcher.UIThread.InvokeAsync(() => {
                IEnumerable<MenuItem> GetAllSubMenuItems(MenuItem menuItem) {
                    return new[] { menuItem }.Concat(
                        menuItem.Items.OfType<MenuItem>().SelectMany(subMenuItem => GetAllSubMenuItems(subMenuItem)));
                }
                var menu = nativeAPI.ViewRender.Host.ContextMenu;
                var allMenuItems = menu.Items.OfType<MenuItem>().SelectMany(item => GetAllSubMenuItems(item));
                var clickedMenuItem = allMenuItems.FirstOrDefault(item => item.GetHashCode() == menuClicked.MenuClicked);
                if (clickedMenuItem != null) {
                    clickedMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                }
                menu.Close();
            });
        }

        void SendWebSocketMessage(Operation operation, string value, string arguments = "[]") {
            SendMessage(nativeAPI.nativeObjectName, $"{{ \"{operation}\": \"{JsonEncodedText.Encode(value)}\", \"Arguments\":{arguments} }}");
        }

        void SendWebSocketMessageRegister(string name, object objectToBind) {
            SendMessage(nativeAPI.nativeObjectName, $"{{ \"{Operation.RegisterObjectName}\": \"{name}\", \"Object\": {SerializeObject(objectToBind)} }}");
        }

        void SendWebSocketMessageEvaluate(string method, string evaluateKey, object[] args) {
            SendMessage(nativeAPI.nativeObjectName, $"{{ \"{Operation.EvaluateScriptFunctionWithSerializedParams}\": \"{JsonEncodedText.Encode(method)}\", \"EvaluateKey\":\"{evaluateKey}\", \"Arguments\":{JsonSerializer.Serialize(args)} }}");
        }
    }
}
