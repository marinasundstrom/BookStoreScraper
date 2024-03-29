﻿# Book Scraper

Console app that scrapes [Books to Scrape](http://books.toscrape.com/), and saves the content to the computer so that you can browse the site offline.

The project is coded in C#/.NET, and for HTML parsing it uses [AngleSharp](https://anglesharp.github.io/).

After running, check the "Output" directory, next to the executable, for the result.

Check [issues](https://github.com/marinasundstrom/BookStoreScraper/issues) for notes.

Refactoring ([#1](https://github.com/marinasundstrom/BookStoreScraper/issues/1)) is on-going in [develop](https://github.com/marinasundstrom/BookStoreScraper/tree/develop) branch.

![Screenshot](screenshot.png)

## Overview 

The program downloads the full website (*). It fetches each page and scans for ```a```, ```link```, ```script``` and ```ìmg``` elements, and then downloads referenced files into the local file system. The files will retain their logical location and the site will be fully browsable from your computer without an Internet connection.

Progress will be output to the console.

### Directive

To properly implement the solution, but not make it too complex for the task to be solved. Following general practices and implement sufficient error handling. The program should generally not fail or crash without hinting a reason.

## Details

### Scraping

It starts with the index page where you can paginate through the entire book collection, and there is a sidebar displaying all categories.

First, the program downloads all the static assets (CSS, JavaScript and images). It makes sure just to download them once. JavaScript which are hosted at another place will not be downloaded.

We scan the current page for anchor elements (```a``` element). That leads to the next page that is going to be scraped. In principle, each page is processed the same way, be it an index page, a category page, or a book page. And a page is only downloaded and processed once.

The main element of interest on the index page is the sidebar containing the categories, so we make sure to prioritize those links, the pages, and contents on them.

Book pages are no different. But we make sure not to process any book links in the "Recently viewed" section because those paths are getting processed via the index and category pages.

### Design details

[AngleSharp](https://anglesharp.github.io/) was chosen for HTML parsing, instead of alternatives as [HTML Agility Pack (HAP)](https://html-agility-pack.net/). AngleSharp is a fast parsing library that provides an API that is closer to the browser's DOM API. It also uses native HTML tag selectors for selecting elements. By contrast, HAP uses XPath by default.

For URL resolving, the processor uses a history stack to keep track of the pages where it has been.

### Error handling

Error messages are output to the console using the default logger. 

At the end of the program, the names of the files that failed to download are saved to the file "failedDownloadsUris.txt". You can then check the log to see the reason why it failed.

## What I’ve learned

The assignment gave me the following insights:

* How to manipulate URIs using the ```Uri``` class in combination with the ```Path``` class. Concatenating the base URI with path ```../``` using ```Path.Combine```.

* Knowledge about how ```Directory.Create``` works: It creates the directories in the specified path if they don’t already exist.

* File paths are specific to the operating system - different rules and prefereed path separators on MacOS, Windows, and Linux. This is not news, but it should be taken into account, for instance, when doing path manipulation using file paths.

If you are building cross-platform apps:

* Test your app on other platforms - not just your own. This app what developed on a Mac, and crashed on Windows because of the reason described above.

## Possible improvements

Here are some things that could be improved:

* Finding out how to fetch _"FontAwesome"_ (a local file) which is referenced in the CSS files.

* Enable parallel processing using functional programming patterns. It would cut execution time.

* Extract certain parts of the code into their own units to make them testable.
    * Make the document scraper and each "processor" (for Script, Anchor, Link, Img) their own classes.

* Additional error handling and recovery

* Benchmarking to find alternative efficient ways of doing stuff - minimize memory allocations, and again, speed up execution.
