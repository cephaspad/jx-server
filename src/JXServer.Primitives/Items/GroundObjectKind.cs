namespace JXServer.Primitives.Items;

/// <summary>
/// Ground object classification (<c>Obj_Kind_*</c> in <c>KObj.h</c> / <c>item-catalog.schema.json</c>).
/// </summary>
public enum GroundObjectKind : byte
{
    Item = 0,
    Money = 1,
    Prop = 2,
    Box = 3,
    Switch = 4,
    Door = 5,
    TrapObj = 6,
}
