# Book Scraper

Console app that scrapes [Books to Scrape](http://books.toscrape.com/), and saves the content to the computer so that you can browse the site offline.

The project is coded in C#/.NET, and for HTML parsing it uses [AngleSharp](https://anglesharp.github.io/).

After running, check the "Output" directory, next to the executable, for the result.

## Details 

The program downloads the full website (*). It fetches each page and scans for ```a```, ```link```, ```script``` and ```ìmg``` elements, and then downloads referenced files into the local file system. The files will retain their logical location and the site will be fully browsable from your computer without an Internet connection.

Progress will be output to the console.

### How it works

It starts with the index page where you can paginate through the entire book collection, and there is a sidebar displaying all categories.

First, the program downloads all the static assets (CSS, JavaScript and images). It makes sure just to download them once. JavaScript which are hosted at another place will not be downloaded.

We scan the current page for anchor elements (```a``` element). That leads to the next page that is going to be scraped. In principle, each page is processed the same way, be it an index page, a category page, or a book page. And a page is only downloaded and processed once.

The main element of interest on the index page is the sidebar containing the categories, so we make sure to prioritize those links, the pages, and contents on them.

Book pages are no different. But we make sure not to process any book links in the "Recently viewed" section because those paths are getting processed via the index and category pages.

### Design details

[AngleSharp](https://anglesharp.github.io/) was chosen for HTML parsing, instead of alternatives as [HTML Agility Pack (HAP)](https://html-agility-pack.net/). AngleSharp is a fast parsing library that provides an API that is closer to the browser's DOM API. It also uses native HTML tag selectors for selecting elements. By contrast, HAP uses XPath by default.

For URL resolving, the processor uses a history stack to keep track of the pages where it has been.

There is also a list of paths that have been processed which makes sure pages get processed only once during a run.

## What I’ve learned

The assignment gave me the following insights:

* How to manipulate URIs using the ```Uri``` class in combination with the ```Path``` class. Concatenating the base URI with path ```../``` using ```Path.Combine```.

* Knowledge about how ```Directory.Create``` works: It creates the directories in the specified path if they don’t already exist.

## Possible improvements

Here are some things that could be improved:

* Finding out how to fetch _"FontAwesome"_ (a local file) which is referenced in the CSS files.

* Enable parallel processing using functional programming patterns. 

* Extract certain parts of the code, make it mockable and testable.

* Additional error handling and recovery

* Benchmarking

## Investigate

Some points worth investigating:

* How performant is the "readPages" HashSet solution? Memory allocations. How does it compare to just checking if a file exists in the file system with ```File.Exists(path)``` ? This method is used in some places.