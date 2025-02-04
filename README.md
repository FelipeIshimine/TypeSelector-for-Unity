# Type Selector for Unity

![Unity_Sm42mt0HKq](https://github.com/user-attachments/assets/653679e3-e69b-4dd0-a892-5635d0306329)

## Overview

The Type Selector package provides a custom Property Attribute with a custom Property Drawer that allows users to select and instantiate subclasses of a given abstract or base class within the Unity Inspector. This is particularly useful for working with `SerializedReference` fields and enables a more user-friendly workflow when dealing with polymorphic objects.

## Features

- **Custom PropertyDrawer:** Easily select types directly in the Inspector.
- **SerializedReference Support:** Works seamlessly with Unity's `SerializedReference` system.
- **Multiple Draw Modes:**
  - **Default:** Standard foldout-based UI.
  - **NoFoldout:** Displays all properties inline without a foldout.
  - **Inline:** Draws properties in a minimalistic layout.
- **Type Filtering:** Only allows valid subclasses.
- **Custom Naming:** Optionally assign custom display names for type selection.
- **Serialization Safety:** Supports missing reference cleanup to ensure robust serialization.

## Usage

### 1. Applying the TypeSelector Attribute

To use the Type Selector, apply the `[TypeSelector]` attribute to a `SerializedReference` field:

```csharp
using TypeSelector;
using UnityEngine;


public class ExampleBehaviour : MonoBehaviour
{
    [SerializeReference, TypeSelector, Tooltip("Select a subclass of ExampleAbstractClass")]
    public ExampleAbstractClass abstractField;
}
```

```csharp
2. Creating an Abstract Base Class
using UnityEngine;

public abstract class ExampleAbstractClass
{
    public abstract void DoSomething();
}
```
### 3. Creating Subclasses
```csharp
public class ExampleConcreteClassA : ExampleAbstractClass
{
    public override void DoSomething()
    {
        Debug.Log("ExampleConcreteClassA is doing something!");
    }
}

public class ExampleConcreteClassB : ExampleAbstractClass
{
    public override void DoSomething()
    {
        Debug.Log("ExampleConcreteClassB is doing something else!");
    }
}
```

### 4. Using Custom Display Names
You can specify a custom display name for your classes using [TypeSelectorName]:

```csharp
using TypeSelector;

[TypeSelectorName("Custom Class A")]
public class CustomNameExampleClass : ExampleAbstractClass
{
    public override void DoSomething() => Debug.Log("Custom Class A Selected");
}
```
### 5. Changing the Draw Mode

Available DrawMode options:
Default: Standard foldout-based display.
NoFoldout: Shows properties directly, without a foldout.
Inline: Displays properties with a minimal layout.

```csharp
[SerializeReference, TypeSelector(DrawMode.Default)]
public ExampleAbstractClass noFoldoutField;

[SerializeReference, TypeSelector(DrawMode.NoFoldout)]
public ExampleAbstractClass noFoldoutField;

[SerializeReference, TypeSelector(DrawMode.Inline)]
public ExampleAbstractClass noFoldoutField;
```

## Notes

SerializedReference: Ensure that the fields using [TypeSelector] are marked with [SerializeReference] for proper functionality.
Type Filtering: The package automatically filters out invalid types (e.g., abstract classes or Unity object types).
Serialization Cleanup: Any missing serialization references will be automatically cleared.


