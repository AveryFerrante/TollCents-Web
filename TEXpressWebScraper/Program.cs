﻿using GoogleApi.Entities.Maps.DistanceMatrix.Response;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;
using System.Text.Json;
using TollCents.Core.Integrations.TEXpress.Entities;

namespace TEXpressWebScraper
{
    internal class Program
    {
        public static Dictionary<string, string> tollSegmentOptionsSelect = new Dictionary<string, string>()
        {
            { "Loop 12 to Eastbound 635", "277" },
            { "Westbound 635 to Loop 12", "278" },
            { "35 to Dallas North Tollway", "279" },
            { "Dallas North Tollway to 35", "280" },
            //{ "Dallas North Tollway to Greenville", "281" },
            //{ "Greenville to Dallas North Tollway", "282" },
        };
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var segments = configuration
                .GetSection("TEXpressSegments")
                .Get<List<TEXpressSegmentWebScraper>>();

            var filePath = configuration.GetValue<string>("OutputFilePath");
            var fileCreationTest = configuration.GetValue<bool>("RunFileCreationTest");

            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(filePath);

            if (fileCreationTest)
            {
                File.WriteAllText(filePath, "This is a test to see if the file is created and written to");
                return;
            }

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            using (IWebDriver driver = new ChromeDriver(options))
            {
                driver.Navigate().GoToUrl("https://www.texpresslanes.com/pricing/calculator/");
                Thread.Sleep(5000);
                segments.ForEach(segment =>
                {
                    var result = RunSeleniumNew(driver, segment.TEXpressCrawlerOptionsSelectValue);
                    var timePrices = ParseTimePrice(result, segment.Description ?? "");
                    segment.TimeOfDayPricing = timePrices;
                    Thread.Sleep(10000);
                });
                File.WriteAllText(filePath, JsonSerializer.Serialize(segments.Cast<TEXpressSegment>(), new JsonSerializerOptions { WriteIndented = true }));
                driver.Quit(); 
            }
        }

        public static string RunSeleniumNew(IWebDriver driver, string segmentValue)
        {
            IWebElement dropdownElement = driver.FindElement(By.Name("tollsegment"));
            var selectElement = new SelectElement(dropdownElement);
            selectElement.SelectByValue(segmentValue);
            Thread.Sleep(4000);

            dropdownElement = driver.FindElement(By.Name("day"));
            selectElement = new SelectElement(dropdownElement);
            selectElement.SelectByValue("monday");
            Thread.Sleep(5000);


            dropdownElement = driver.FindElement(By.Name("time-hour"));
            selectElement = new SelectElement(dropdownElement);
            selectElement.SelectByValue("07:00:00");
            Thread.Sleep(3500);

            IWebElement acceptCookiesButton = driver.FindElement(By.Id("wt-cli-accept-btn"));
            if (acceptCookiesButton is not null && acceptCookiesButton.Displayed)
            {
                acceptCookiesButton.Click();
                Thread.Sleep(2200);
            }
            
            IWebElement submitButton = driver.FindElement(By.XPath("//input[@type='submit' and @value='Get average price']"));
            driver.ExecuteJavaScript("arguments[0].scrollIntoView(true);", submitButton);
            submitButton.Click();
            Thread.Sleep(7500);

            var viewCompleteTableButton = driver.FindElement(By.LinkText("View complete table"));
            driver.ExecuteJavaScript("arguments[0].scrollIntoView(true);", viewCompleteTableButton);
            viewCompleteTableButton.Click();
            Thread.Sleep(1000);

            var table = driver.FindElement(By.XPath("//table[@class='table table-timetable']"));
            var raw = table.GetAttribute("innerHTML");

            Thread.Sleep(2000);
            var getNewPriceButton = driver.FindElement(By.XPath("//button[text()='Get new average price']"));
            driver.ExecuteJavaScript("arguments[0].scrollIntoView(true);", getNewPriceButton);
            getNewPriceButton.Click();

            return raw ?? "";
        }

        public static Dictionary<string, IEnumerable<TimePrice>> ParseTimePrice(string htmlData, string tollSegment)
        {
            var doc = new HtmlDocument();
            if (!string.IsNullOrWhiteSpace(htmlData))
                doc.LoadHtml(htmlData);
            else
            {
                Console.WriteLine("HTML STRING IS NULL FOR " + tollSegment);
                return new Dictionary<string, IEnumerable<TimePrice>>();
            }

            var something = doc.DocumentNode.SelectSingleNode("tbody");
            var tableRows = something.SelectNodes("tr");

            var dictionay = new Dictionary<string, IEnumerable<TimePrice>>();

            try
            {
                foreach (var row in tableRows)
                {
                    var tableHeader = row.SelectSingleNode("th");
                    var time = tableHeader.InnerText.Trim();

                    var cells = row.SelectNodes("td");
                    cells.ToList().ForEach(cell =>
                    {
                        var dayOfWeek = cell.GetAttributeValue("data-heading", "");
                        var priceDirty = cell.SelectSingleNode("div").InnerText.Trim().Substring(1);
                        var price = double.Parse(priceDirty);
                        if (!dictionay.ContainsKey(dayOfWeek))
                        {
                            dictionay[dayOfWeek] = new List<TimePrice>()
                            {
                                new TimePrice
                                {
                                    Time = time,
                                    Price = price
                                }
                            };
                        }
                        else
                        {
                            dictionay[dayOfWeek] = dictionay[dayOfWeek].Append(new TimePrice
                            {
                                Time = time,
                                Price = price
                            });
                        }
                    });
                }
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Error parsing html" + tollSegment);
            }

            return dictionay;
        }
    }
}
