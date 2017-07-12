﻿using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class IsolatedJavascriptEvaluation : TestBase {

        protected override bool ReuseWebView {
            get { return false; }
        }

        protected override bool WaitReady {
            get { return false; }
        }

        [Test(Description = "Evaluation timeouts when javascript engine is not initialized")]
        public void JavascriptEngineInitializationTimeout() {
            LoadAndWaitReady("<html><body></body></html>");
            var exception = Assert.Throws<WebView.JavascriptException>(() => TargetWebView.EvaluateScript<int>("1", TimeSpan.FromSeconds(1)));
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.Message.Contains("not initialized"));
        }

        [Test(Description = "Method interception function is called")]
        public void RegisteredJsObjectMethodInterception() {
            const string DotNetObject = "DotNetObject";
            var functionCalled = false;
            var interceptorCalled = false;
            Func<int> functionToCall = () => {
                functionCalled = true;
                return 10;
            };
            Func<Func<object>, object> interceptor = (originalFunc) => {
                interceptorCalled = true;
                return originalFunc();
            };
            TargetWebView.RegisterJavascriptObject(DotNetObject, functionToCall, interceptor);
            LoadAndWaitReady("<html><script>DotNetObject.invoke();</script><body></body></html>");
            WaitFor(() => functionCalled, TimeSpan.FromSeconds(2));
            Assert.IsTrue(functionCalled);
            Assert.IsTrue(interceptorCalled);
        }
    }
}