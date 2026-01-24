using UnityEngine;

/// <summary>
/// Pakottaa enum-kent‰n piirtym‰‰n maskina (multi-select) Inspectorissa,
/// vaikka Unity piirt‰isi sen muuten yksivalintaisena.
/// </summary>
public sealed class EnumFlagsAttribute : PropertyAttribute { }
