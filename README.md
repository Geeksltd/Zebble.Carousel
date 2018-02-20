[logo]: https://raw.githubusercontent.com/Geeksltd/Zebble.Carousel/master/Shared/NuGet/Icon.png "Zebble.Carousel"


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
