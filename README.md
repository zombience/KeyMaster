# KeyMaster
Keymaster is a tool that allows for command trigger assignment with minimal code. Commands can either be triggered by key presses or from network messages. 

## Overview

### keymaster is a bit of code that allows you to
* implement trigger commands with minimal code
* assign key values to triggers using optional key-combos (such as shift + alt)
* export command assignments to json which can be used by an external application to send commands remotely

### keymaster is useful for
* quick prototyping of trigger-based functionality
* creating categories for triggers
* setting game states
* spoofing user interaction
* pretty much whatever

### keymaster is NOT
* a feature-heavy input framework
* intended for use in final builds
* intended to replace a real input framework (i.e. probably don't use this in place of real user keyboard input modules)
* guaranteed to be bug free
* tested on Linux or OSX (probably just need to fix path slash orientation and KeyCode.Alt -> Keycode.Command for OSX)

### keymaster exists because
For those that use Unity3D for site-specific installations, the need for setting states, testing basic user input, checking game responses to known input, etc. is invaluable. It can be a pain to get that kind of thing set up. I've used this on several projects and it has helped immensely. I use the keyboard input when I'm developing at my desk. When I'm working with target hardware, I use a UI developed in Touchdesigner that sends messages via network. (Once I update that project I'll add a link here)

## Usage

### ControlPage enum

The first step is to define which categories (pages) you'd like to use. You can modify the enum at any time. 
The ControlPage enum can be found in file: 

KeyMasterBase.cs

and comes with the default Pages:

<html>
<pre>
<code>
public enum ControlPage
{
    None            = 0,
    // page selection will always be active   
    PageSelection   = 1, 
    Dev             = 2,
    Scene           = 3, 
    UI              = 4,
    Audio           = 5,
}
</code>
</pre>
</html>

the enum values None and PageSelection are **required**. All other values can be modified or removed as you need. 

Once you've added the pages that you would like to use, you will need to set up the triggers which allow you to switch between categories. If you have a very small project you could potentially get away with one category. In my experience, however, you'll run out of keyboard keys very quickly on a more complex project, so starting off with more categories is a good idea. You can use the same key assignment on different pages without conflict. 

The code which handles PageSelection can be found in file
KeyMaster.cs around line 338


The example code below shows each page controlled by the number row keys 0 - 6.
Change these key assignments if you like. If you are using remote control with this, I would strongly recommend renaming the methods to reflect the pages you have defined in your enum. Those method names are the only identifying field when generating json. If you don't name these methods clearly you'll get confusing names in your json file. 

<html>
<pre>
<code>
internal class PageSelector : IKeyHolder
{

    public ControlPage Page => ControlPage.PageSelection;
    IKeyMaster KeyMaster => _keyMaster ?? (_keyMaster = _keyMaster.Get());
    IKeyMaster _keyMaster;

    internal PageSelector()
    {
        KeyMaster.RegisterKeyholder(this);
    }

    [KeyToken(ControlPage.PageSelection, KeyCode.Alpha0)]
    void DeactivateAllKeyControls()
    {
        KeyMaster.SetActivePage(ControlPage.None);
    }

    [KeyToken(ControlPage.PageSelection, KeyCode.Alpha1)]
    void SetDevActive()
    {
        KeyMaster.SetActivePage(ControlPage.Dev);
    }

    [KeyToken(ControlPage.PageSelection, KeyCode.Alpha2)]
    void SetSceneActive()
    {
        KeyMaster.SetActivePage(ControlPage.Scene);
    }

    [KeyToken(ControlPage.PageSelection, KeyCode.Alpha3)]
    void SetUIActive()
    {
        KeyMaster.SetActivePage(ControlPage.UI);
    }

    [KeyToken(ControlPage.PageSelection, KeyCode.Alpha4)]
    void SetAudioActive()
    {
        KeyMaster.SetActivePage(ControlPage.Audio);
    }
}
</code>
</pre>
</html>


### Creating triggers

First, make sure to include the line
<html>
<pre>
<code>
using KeyMaster;
</code>
</pre>
</html>

at the top of your cs file so that you have access to KeyMaster classes. 

Next create a class to contain your trigger methods. You can either inherit from the **KeyHolderBase** class if you want an object in your scene to allow for inspector assignments, or you can directly implement the **IKeyHolder** interface if you are adding these methods to pre-existing classes in your project. 

The only difference is that **KeyHolderBase** will automagically register its triggers with the framework from the base class' Start() method. 

#### Inherit from KeyHolderBase example

Create your class and add a method

<html>
<pre>
<code>
using KeyMaster;

public class MyAmazingClass : KeyHolderBase
{

	void ReportStatus()
	{
		Debug.LogFormat("things are pretty ok I guess");
	}
}
</code>
</pre>
</html>

Decorate your method with the KeyToken attribute. 
The KeyToken attribute takes 2 required args and 1 optional arg:

page: 	used to enable/disable these triggers appropriately when selected page changes  
key: 	if using keyboard input, used to listen for trigger
combo: 	(optional) add modifier keys (alt, shift, ctrl). if not adding mod keys, this trigger **will not** trigger if **any** mod keys are held down. This is so that it is possible to use the same key on the same page, for different triggers. See caveat section above. 
<html>
<pre>
<code>
using KeyMaster;

public class MyAmazingClass : KeyHolderBase
{

	// not using key combo for now
	[KeyToken(ControlPage.Dev, KeyCode.R)]
	void ReportStatus()
	{
		Debug.LogFormat("things are pretty ok I guess");
	}
}
</code>
</pre>
</html>

Add a GameObject to your Unity scene, and add the MyAmazingClass component to it. 
That's it, you're done. 
Enter play mode, and press number row key Alpha1 (1) to activate the Dev page. Now pressing the R key should output the above message in the console. 

#### Implement IKeyHolder interface example

Pretty much the same as above, except you must manually regsiter your class' triggers. Luckily the process is pretty simple:

<html>
<pre>
<code>
using KeyMaster;

public class TriggerContainer : IKeyHolder
{
    [KeyToken(ControlPage.Dev, KeyCode.T)]
    void Triggered()
    {
        Debug.LogFormat("triggered");
    } 
}


public class SomeManagerClass : Monobehaviour
{
	
    TriggerContainer triggers;

    void Start()
	{
		triggers.RegisterKeyHolder();
	}
}
</code>
</pre>
</html>

As long as SomeManagerClass is in the scene and active, it will register your TriggerContainer class and your triggers will become available when the Dev page is in focus. 

That's it! Happy trails. 

## Viewing Triggers
Use Unity's dropdown menu to select the menu option: 
Utilities > KeyMaster > View Command Map

A window will open up showing all key assignments in the project. You can filter by type (class), page, or key. 


## Known Issues

### Command Map Window throws errors after recompile
If the Command Map window is open when recompiling, references will be lost on compile and errors will pop up in the log. Simply click the repopulate button at the top of the window. I may fix this eventually.  

### Triggers without a KeyCombo assigned will not be triggered when holding down a modifier key (alt, shift, ctrl): 
The usage of Key Combos for additional controls prevents usage of shift-accessible keys. 
For example, it is possible to use code to assign the plus key + to a trigger. getting a keyboard to emit this key, however, requires that the shift key be held down in combination with the equals = key. In this scenario, the trigger listening for the + key will never execute. The code is written in such a way that in order to allow for doubling of key assignments with mod keys, the non-mod key commands must be prevented if any mod keys are held down. Otherwise both the key trigger without a combo and the key trigger with a combo would execute. This could probably be fixed with a fancier registration and sorting process, but there's only so many hours in the day, and it's not that big a deal for my use cases. 
 
## Future Work

The original framework was developed inside a complex project with a lot of dependencies. At some point I will extract the networking portion of that project and include it here. It's very useful. In the meantime the functionality exists to generate json code that can be used to populate your own remote trigger app if you want to work on it. Otherwise I'll extract that code hopefully soon. 

## ENJOY!
I hope you find this useful. 
Please use and share freely, attribution if you like. Open to pull requests and suggestions. 

And of course, there is only Zuul
<html><a href="http://jasonaraujo.net"><img src="http://jasonaraujo.net/wp-content/uploads/2017/keymaster.gif"/></a>