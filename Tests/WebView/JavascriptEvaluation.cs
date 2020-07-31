﻿using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using JavascriptException = WebViewControl.WebView.JavascriptException;

namespace Tests.WebView {

    [Timeout(10000)]
    public class JavascriptEvaluation : WebViewTestBase {

        [Test(Description = "A simple script evaluates correctly")]
        public async Task EvaluateSimpleScript() {
            await Run(() => {
                var result = TargetView.EvaluateScript<int>("2+1");
                Assert.AreEqual(3, result);
            });
        }

        [Test(Description = "The order of the executed scripts is respected")]
        public async Task ExecutionOrderIsRespected() {
            await Run(() => {
                try {
                    TargetView.ExecuteScript("x = ''");
                    var expectedResult = "";
                    // queue 10000 scripts
                    for (var i = 0; i < 10000; i++) {
                        TargetView.ExecuteScript($"x += '{i},'");
                        expectedResult += i + ",";
                    }
                    var result = TargetView.EvaluateScript<string>("x");
                    Assert.AreEqual(expectedResult, result);

                    TargetView.ExecuteScript("x = '-'");
                    result = TargetView.EvaluateScript<string>("x");
                    Assert.AreEqual("-", result);

                } finally {
                    TargetView.EvaluateScript<bool>("delete x");
                }
            });
        }

        [Test(Description = "Evaluation of complex objects returns the expected results")]
        public async Task ComplexObjectsEvaluation() {
            await Run(() => {
                var result = TargetView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32, Parent: { Name: 'Snows Parent', Age: 60 }, Kind: 2 })");
                Assert.IsNotNull(result);
                Assert.AreEqual("Snows", result.Name);
                Assert.AreEqual(32, result.Age);
                Assert.IsNotNull(result.Parent);
                Assert.AreEqual("Snows Parent", result.Parent.Name);
                Assert.AreEqual(60, result.Parent.Age);
                Assert.AreEqual(Kind.C, result.Kind);
            });
        }

        [Test(Description = "Evaluation of scripts with errors returns stack and message details")]
        public async Task EvaluationErrorsContainsMessageAndJavascriptStack() {
            await Run(() => {
                var exception = Assert.Throws<JavascriptException>(() => TargetView.EvaluateScript<int>("(function foo() { (function bar() { throw new Error('ups'); })() })()"));

                Assert.AreEqual("Error: ups", exception.Message);
                var stack = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                Assert.Greater(stack.Length, 2);
                Assert.True(stack.ElementAt(0).StartsWith("   at bar in about"));
                Assert.True(stack.ElementAt(1).StartsWith("   at foo in about"));
            });
        }

        [Test(Description = "Evaluation of scripts includes evaluated function but not args")]
        public async Task EvaluationErrorsContainsEvaluatedJavascript() {
            await Run(() => {
                var exception = Assert.Throws<JavascriptException>(() => TargetView.EvaluateScriptFunction<int>("Math.min", "123", "(function() { throw new Error() })()"));

                var stack = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                Assert.Greater(stack.Length, 1);
                Assert.True(stack.ElementAt(0).StartsWith("   at Math.min in eval"));
                Assert.IsFalse(stack.ElementAt(0).Contains("123"));
            });
        }

        [Test(Description = "Evaluation of scripts with comments, json objects, and var declarations")]
        public async Task ScriptsWithComplexSyntaxAreEvaluated() {
            await Run(() => {
                var result = TargetView.EvaluateScript<int>("2+1 // some comments");
                Assert.AreEqual(3, result);

                result = TargetView.EvaluateScript<int>("var x = 1; 5");
                Assert.AreEqual(5, result);

                var resultObj = TargetView.EvaluateScript<TestObject>("({ Name: 'Snows', Age: 32})");
                Assert.IsNotNull(resultObj);
            });
        }

        [Test(Description = "Evaluation of scripts timesout after timeout elapsed")]
        public async Task EvaluationTimeoutIsThrown() {
            await Run(() => {
                var exception = Assert.Throws<JavascriptException>(
                () => TargetView.EvaluateScript<int>("var start = new Date().getTime(); while((new Date().getTime() - start) < 150);",
                timeout: TimeSpan.FromMilliseconds(50)));
                Assert.True(exception.Message.Contains("Timeout"));
            });
        }

        [Test(Description = "Evaluation of null returns empty array when result is array type")]
        public async Task EvaluationReturnsEmptyArraysWhenNull() {
            await Run(() => {
                var result = TargetView.EvaluateScript<int[]>("null");
                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Length);
            });
        }

        [Test(Description = "Unhandled Exception event is called when an async error occurs")]
        public async Task UnhandledExceptionEventIsCalled() {
            await Run(() => {
                const string ExceptionMessage = "nooo";

                Exception exception = null;

                var controlUnhandled = false;
                var dispatcherUnhandled = false;
                var markAsHandled = true;
                var taskCompletionSource = new TaskCompletionSource<bool>();

                void AssertResult(int result) {
                    Assert.NotNull(exception);
                    Assert.IsTrue(exception.Message.Contains(exception.Message));
                    Assert.AreEqual(2, result, "Result should not be affected");
                }

                void OnUnhandledDispatcherException(object o, UnhandledExceptionEventArgs e) {
                    exception = e.ExceptionObject as Exception;
                    dispatcherUnhandled = true;
                    taskCompletionSource.SetResult(true);
                }

                AppDomain.CurrentDomain.UnhandledException += OnUnhandledDispatcherException;

                WithUnhandledExceptionHandling(() => {
                    try {
                        TargetView.ExecuteScript($"throw new Error('{ExceptionMessage}')");
                        var result = TargetView.EvaluateScript<int>("1+1"); // force exception to occur

                        AssertResult(result);
                        Assert.IsTrue(controlUnhandled);
                        Assert.IsFalse(dispatcherUnhandled);

                        controlUnhandled = false;
                        markAsHandled = false;

                        TargetView.ExecuteScript($"throw new Error('{ExceptionMessage}')");
                        result = TargetView.EvaluateScript<int>("1+1"); // force exception to occur

                        Task.WaitAll(taskCompletionSource.Task);

                        AssertResult(result);
                        Assert.IsTrue(controlUnhandled);
                        Assert.IsTrue(dispatcherUnhandled);

                    } finally {
                        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledDispatcherException;
                    }
                },
                e => {
                    exception = e;
                    controlUnhandled = true;
                    return markAsHandled;
                });
            });
        }

        [Test(Description = "Javascript errors that occur asyncrounsly throw unhandled exception")]
        public async Task JavascriptAsyncErrorsThrowUnhandledException() {
            await Run(() => {
                const string ExceptionMessage = "nooo";
                var taskCompletionSource = new TaskCompletionSource<bool>();

                Exception exception = null;

                WithUnhandledExceptionHandling(() => {
                    TargetView.ExecuteScript($"function foo() {{ throw new Error('{ExceptionMessage}'); }}; setTimeout(function() {{ foo(); }}, 1); ");

                    Task.WaitAll(taskCompletionSource.Task);

                    Assert.IsTrue(exception.Message.Contains(ExceptionMessage), "Found " + exception.Message);
                    var stack = exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    Assert.AreEqual(2, stack.Length);
                    Assert.True(stack.ElementAt(0).StartsWith("   at foo in about:blank:line 1"), "Found " + stack.ElementAt(0));
                    Assert.True(stack.ElementAt(1).StartsWith("   at <anonymous> in about:blank:line 1"), "Found " + stack.ElementAt(1));
                },
                e => {
                    exception = e;
                    taskCompletionSource.SetResult(true);
                    return true;
                });
            });
        }
    }
}
