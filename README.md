[logo]: https://raw.githubusercontent.com/Geeksltd/Zebble.Carousel/master/icon.png "Zebble.Carousel"


## Zebble.Carousel

![logo]

Carousel plugin allows the user to swipe from side to side to navigate through views, like a gallery slider.


[![NuGet](https://img.shields.io/nuget/v/Zebble.Carousel.svg?label=NuGet)](https://www.nuget.org/packages/Zebble.Carousel/)

<br>


### Setup
* Available on NuGet: [https://www.nuget.org/packages/Zebble.Carousel/](https://www.nuget.org/packages/Zebble.Carousel/)
* Install in your platform client projects.
* Available for iOS, Android and UWP.

<br>


### Api Usage


```xml
<Carousel Id="MyCarousel">
	<ImageView Path="..../Slide1.png" />
	<ImageView Path="..../Slide2.png" />
	<ImageView Path="..../Slide3.png" />
	...
</Carousel>
```

For adding a slide view in code behind to you Carousel use AddSlide(myView) method.

```csharp
MyCarousel.AddSlide(new Canvas());
```

You can style the Carousel-Bullet and it's active state like this:

```css
Carousel-Bullet{ 
	background-color:#eee;
	  &:active{ background-color:#333; 
	  } 
	}
```

#### Dynamic data source

In the above example, you can use a <z-foreach> loop to dynamically create slides from a data source. For instance, the following code will show a slide for each image file inside the MySlides folder in the application resources:

```xml
<Carousel Id="MyCarousel">
	<z-foreach var="file" in="@GetSlideFiles()">
	   <ImageView Path="@file" />
	</z-foreach>
    <AnyOtherView />
</Carousel>
```
Code behind:

```csharp
IEnumerable<string> GetSlideFiles()
{
     return Device.IO.Directory("Images/MySlides").GetFiles().Select(x => x.FullName);
}
```

<br>


### Properties
| Property     | Type         | Android | iOS | Windows |
| :----------- | :----------- | :------ | :-- | :------ |
| CenterAligned   | bool         | x       | x   | x       |
| SlideWidth   | float?         | x       | x   | x       |
| EnableZooming   | bool         | x       | x   | x       |


<br>


### Methods
| Method       | Return Type  | Parameters                          | Android | iOS | Windows |
| :----------- | :----------- | :-----------                        | :------ | :-- | :------ |
| AddSlide        | Task<Slide>         | View => child | x       | x   | x       |
| Next        | Task         |	bool => animate	| x       | x   | x       |
| Previous   | Task         | bool => animate | x       | x   | x       |

# RecyclerCarousel
Normal carousel is very flexible. Each slide can be any view object. But it requires all slides to be pre-rendered, which is not efficient if you have many.

In cases where you have several views all with the same templates, it's much more efficient to use `RecyclerCarousel` which is much much faster.

```html
<RecyclerCarousel z-of="Product, SlideTemplate" DataSource="@Products">
    <z-Component z-type="SlideTemplate" z-base="RecyclerCarouselSlide[Product]">
        <ImageView Path="@{Item, x=>x?.Thumbnail}" />
        <TextView  Text="@{Item, x=>x?.Name}"/>
    </z-Component>
</RecyclerCarousel>
```

In the above example, `Products` is an `IEnumerable<Product>` which is the data source from which to populate the slides. For example you may have 10 product instances in an array. The carousel will always render a maximum of 3 items. As you swipe through the slides, it will reuse the slide ui objects and just change their X position and also update the data source which is the `Item` property. Please note that `Item` is a `Bindable<Product>` which means you need to use the `@{Item, x=>x.Something}` syntax in your template.
