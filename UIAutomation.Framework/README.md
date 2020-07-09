# UI Automation Framework

## Requirements
To be able to run test you should install WinAppDriver and Sikuli. Installation instructions you can find below.

1. [WinAppDriver](https://github.com/microsoft/WinAppDriver)
2. [Sikuli](https://github.com/VladislavKostyukevich/SikuliSharp.NetCore)

## Settings

Before running any test you should fill settings in `config.json`

```json
{
    "WinAppDriverPath": "PathTo\\WinAppDriver.exe",
    "AppName": "Application Name",
    "Timeout": 20,
    "WindowsApplicationDriverUrl": "URL to WinAppDriverHub",
    "PathToApp" :"PathToAppUnderTest\\Example.Avalonia.exe",
    "DebugPort": 9090,
    "ChromiumVersion": "75.0.3770.90"
}
```

## Test Creation

To create test you just need to extent the `AppSession` class. On setup phase you will need to run `Setup` method and `TearDown` method on TearDown phase

Here is example on Nunit test:

```C#
[TestFixture]
    public class TestScenario : AppSession
    {
        [Test]
        public void ClickSomething()
        {
            // Test code here
        }

        [SetUp]
        public static void SetUp()
        {
            Setup();
        }

        [TearDown]
        public static void ClassCleanup()
        {
            TearDown();
        }
    }
```

## Using Native part of application

Since native part cannot be accessed by any automation framework we are using Sikuli to detect elements and interact with it.

Sikuli using Image recognition for element detection. You can store patterns in `ImageLocators` folder of your test project.

To interact with element create NativeElement object with specifying path to image pattern.

You can use `ResourceUtil.PatternPath` static method with specifying relative path to pattern.

```C#
private readonly NativeElement actionsNative = new NativeElement(ResourceUtil.PatternPath("actions.png"));
```

## Using Web part

The web part can be accessed by using Chrome webdriver.

The webdriver connecting to app using debug port. It provides number of contexts where each context is a tab, so when you changing the tab you also will need to change context (just by using WindowHandles)

You can use WebService to perform Context change:

```C#
WebService.GetInstance().SwitchToTab(index);
```

To interact with element create Element or specific element object (Button, Input and etc.) object with specifying locator and object name.

```C#
private readonly Input notShadowRootInput = new Input(By.XPath("//*[@id='webview_root']/div/input"), "Input");
```

To interact with element in shadow root create Element or specific element object (Button, Input and etc.) object with specifying CSS locator, object name, array of roots locator.

```C#
private readonly Button shadowRootButton = new Button(By.CssSelector("#webview_root > div > button"), "Button", new string[] { "#webview_root > div > div:nth-child(10) > div" });
```


