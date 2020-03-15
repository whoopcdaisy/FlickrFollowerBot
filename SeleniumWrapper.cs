﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;

namespace FlickrFollowerBot
{
	internal class SeleniumWrapper : IDisposable
	{

		private readonly IJavaScriptExecutor JsDriver;

		private static ChromeOptions GetOptions(string w, string h, IEnumerable<string> seleniumBrowserArguments)
		{
			ChromeOptions options = new ChromeOptions
			{
				PageLoadStrategy = PageLoadStrategy.Normal
			};
			options.AddArgument("--window-size=" + w + "," + h); // try to randomize the setup in order to reduce detection
																 // configurable
			foreach (string a in seleniumBrowserArguments)
			{
				options.AddArgument(a);
			}
			return options;
		}

		public static SeleniumWrapper NewChromeSeleniumWrapper(string path, string w, string h, IEnumerable<string> seleniumBrowserArguments, float botSeleniumTimeoutSec)
		{
			ChromeOptions options = GetOptions(w, h, seleniumBrowserArguments);
			return new SeleniumWrapper(new ChromeDriver(path, options), botSeleniumTimeoutSec);
		}

		/// <param name="uri">exemple http://127.0.0.1:4444/wd/hub </param>
		public static SeleniumWrapper NewRemoteSeleniumWrapper(string configUri, string w, string h, IEnumerable<string> seleniumBrowserArguments, float botSeleniumTimeoutSec)
		{
			if (!Uri.TryCreate(configUri, UriKind.Absolute, out Uri uri)) // may be a hostname ?
			{
				uri = new Uri("http://" + configUri + ":4444/wd/hub");
			}
			ChromeOptions options = GetOptions(w, h, seleniumBrowserArguments);
			return new SeleniumWrapper(new RemoteWebDriver(uri, options), botSeleniumTimeoutSec);
		}

		private readonly TimeSpan NormalWaiter;

		private SeleniumWrapper(IWebDriver webDriver, float botSeleniumTimeoutSec)
		{
			NormalWaiter = TimeSpan.FromSeconds(botSeleniumTimeoutSec);

			WebDriver = webDriver;
			webDriver.Manage().Timeouts().PageLoad = NormalWaiter;
			webDriver.Manage().Timeouts().ImplicitWait = NormalWaiter;
			webDriver.Manage().Timeouts().AsynchronousJavaScript = NormalWaiter;

			JsDriver = (IJavaScriptExecutor)webDriver;
		}

		private IEnumerable<IWebElement> FindElementsThatMayBeEmpty(By by)
		{
			try
			{
				return WebDriver.FindElements(by);
			}
			catch
			{
				return Array.Empty<IWebElement>();
			}
		}


		public string Url
		{
			get => WebDriver.Url;
			set => WebDriver.Url = value;
		}
		
		public string Title
		{
			get => WebDriver.Title;
		}
		
		internal string CurrentPageSource
		{
			get => JsDriver.ExecuteScript("return document.documentElement.innerHTML").ToString();
		}

		public IEnumerable<IWebElement> GetElements(string cssSelector, bool displayedOnly = true, bool noImplicitWait = false)
		{
			if (noImplicitWait)
			{
				WebDriver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			}
			IEnumerable<IWebElement> ret = FindElementsThatMayBeEmpty(By.CssSelector(cssSelector));
			if (displayedOnly)
			{
				ret = ret.Where(x => x.Displayed);
			}
			if (noImplicitWait)
			{
				WebDriver.Manage().Timeouts().ImplicitWait = NormalWaiter;
			}
			return ret;
		}

		public IEnumerable<string> GetAttributes(string cssSelector, string attribute = "href", bool displayedOnly = true, bool noImplicitWait = false)
		{
			return GetElements(cssSelector, displayedOnly, noImplicitWait)
				.Select(x => x.GetAttribute(attribute));
		}

		public void ClickIfPresent(string cssSelector)
		{

			WebDriver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
			try
			{
				WebDriver.FindElement(By.CssSelector(cssSelector))
					.Click();
			}
			catch (NoSuchElementException)
			{
				// ignore
			}
			WebDriver.Manage().Timeouts().ImplicitWait = NormalWaiter;
		}

		public void Click(string cssSelector)
		{
			WebDriver.FindElement(By.CssSelector(cssSelector))
				.Click();
		}

		public void InputWrite(string cssSelector, string text)
		{
			WebDriver.FindElement(By.CssSelector(cssSelector))
					.SendKeys(text);
		}

		public void EnterKey(string cssSelector)
		{
			WebDriver.FindElement(By.CssSelector(cssSelector))
					.SendKeys(Keys.Enter);
		}

		internal void ScrollToBottom()
		{
			JsDriver.ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");
		}

		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		internal IEnumerable<object> Cookies
		{
			get => WebDriver.Manage().Cookies.AllCookies;
			set
			{
				foreach (JObject cookie in value.OfType<JObject>())
				{
					WebDriver.Manage().Cookies.AddCookie(
						new Cookie(
							cookie["name"].ToString(),
							cookie["value"].ToString(),
							cookie["domain"].ToString(),
							cookie["path"].ToString(),
							cookie["expiry"] != null ? Epoch.AddMilliseconds(cookie["expiry"].Value<long>()) : null as DateTime?
						)
					);
				}
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls
		private IWebDriver WebDriver;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					try
					{
						WebDriver.Quit();
					}
					catch
					{
						// disposing
					}
					finally
					{
						WebDriver.Dispose();
					}
					WebDriver = null;
				}
				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion

	}
}
