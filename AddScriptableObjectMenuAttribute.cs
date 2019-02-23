using System;

namespace ScriptableObjectCreator
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class AddScriptableObjectMenuAttribute : System.Attribute
	{
		public string menu { get; }

		public AddScriptableObjectMenuAttribute(string menu)
		{
			this.menu = menu;
		}
	}
}