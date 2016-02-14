module ExtensionMethod

type System.String with
  member this.Method(x: int) = ()
  member this.Property with get() = 0 and set(x: string) = ()
  member this.IndexedProperty with get (index: int) = "" and set(index: string) (value: float) = ()

type List<'a> with
  member this.Method() = 0
  member this.Property with get() = 0

type 'a``[]`` with
  member this.Method() = 0
  member this.Property with get() = 0