
using System.Collections.Generic;
using UnityEngine;

public static class SystemExtension
{
    public static T GetRandomItem<T>(this List<T> list)
    {
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    public static bool IsNullOrEmpty(this string selfStr)
    {
        return string.IsNullOrEmpty(selfStr);
    }
    public static bool IsTrimNotNullAndEmpty(this string selfStr)
    {
        return selfStr != null && !string.IsNullOrEmpty(selfStr.Trim());
    }
    public static bool IsNotNullAndEmpty(this string selfStr)
    {
        return !string.IsNullOrEmpty(selfStr);
    }
    public static float ToAngle(this Vector2 self)
    {
        return Mathf.Atan2(self.y, self.x).Rad2Deg();
    }
    public static float Rad2Deg(this float self)
    {
        return self * Mathf.Rad2Deg;
    }
        
    public static float Rad2Deg(this int self)
    {
        return self * Mathf.Rad2Deg;
    }
    public static Vector2 AngleToDirection2D(this int self)
    {
        return new Vector2(self.CosAngle(), self.SinAngle());
    }
        
    public static Vector2 AngleToDirection2D(this float self)
    {
        return new Vector2(self.CosAngle(), self.SinAngle());
    }
    public static float CosAngle(this float self)
    {
        return Mathf.Cos(self * Mathf.Deg2Rad);
    }
        
    public static float CosAngle(this int self)
    {
        return Mathf.Cos(self * Mathf.Deg2Rad);
    }
    public static float SinAngle(this float self)
    {
        return Mathf.Sin(self * Mathf.Deg2Rad);
    }
        
    public static float SinAngle(this int self)
    {
        return Mathf.Sin(self * Mathf.Deg2Rad);
    }
}
