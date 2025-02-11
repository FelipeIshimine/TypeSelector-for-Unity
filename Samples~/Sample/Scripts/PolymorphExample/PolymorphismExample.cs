using System;
using System.Collections.Generic;
using TypeSelector;
using UnityEngine;

public class PolymorphismExample : MonoBehaviour
{
	[Header("Press PLAY, and change the class of the 'animation'\n field by pressing the button on the right of the field")]
	[TypeSelector,SerializeReference] public AnimationBaseClass animation;
	public void Update()
	{
		animation?.Update(gameObject,Time.deltaTime);
	}
}

[System.Serializable]
public abstract class AnimationBaseClass
{
	public abstract void Update(GameObject gameObject, float delta);
}

[System.Serializable, TypeSelectorName("Circular Movement")]
public class CircularMovementAnimation : AnimationBaseClass
{
	[SerializeField] private float speed = 0.25f;
	[SerializeField] private float radius = 3;
	private float time;
	private const float TWO_PI = Mathf.PI * 2;

	public override void Update(GameObject gameObject, float delta)
	{
		time += delta;
		var targetPosition = new Vector3(Mathf.Sin(time * speed * TWO_PI),Mathf.Cos(time * speed * TWO_PI)) * radius;

		var distance = Vector3.Distance(gameObject.transform.position, targetPosition);
		gameObject.transform.localPosition = Vector3.MoveTowards(gameObject.transform.localPosition, targetPosition, delta * distance * 10);
	}
}

[System.Serializable, TypeSelectorName("Scale")]
public class ScaleAnimation : AnimationBaseClass
{
	[SerializeField] private float speed = 1;
	[SerializeField] private float scale = 2;
	
	private float time;
	private const float TWO_PI = Mathf.PI * 2;

	public override void Update(GameObject gameObject, float delta)
	{
		time += delta;
		var targetPosition = Mathf.Sin(time * speed * TWO_PI) * scale * Vector3.one + Vector3.one;
		gameObject.transform.localScale = Vector3.MoveTowards(gameObject.transform.localScale, targetPosition, delta*speed);
	}
}

[System.Serializable, TypeSelectorName("Rotation")]
public class RotateAnimation : AnimationBaseClass
{
	[SerializeField] private float anglesPerSecond = 270;
	public override void Update(GameObject gameObject, float delta) => gameObject.transform.Rotate(Vector3.forward, anglesPerSecond * delta);
}

[System.Serializable, TypeSelectorName("-Multiple-")]
public class AnimationArray : AnimationBaseClass
{
	[SerializeReference,TypeSelector]  public List<AnimationBaseClass> animations = new();
	public override void Update(GameObject gameObject, float delta)
	{
		foreach (AnimationBaseClass animation in animations)
		{
			animation?.Update(gameObject,delta);
		}
	}
}
