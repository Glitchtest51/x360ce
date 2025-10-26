using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using x360ce.App.Input.Devices;

namespace x360ce.App.Input.Triggers
{
	/// <summary>
	/// Handles device selection events and generates device information display.
	/// Extracts detailed device properties and formats them for display in a 3-column layout.
	/// </summary>
	internal class DevicesTab_DeviceSelectedInfo
	{
		private readonly UnifiedInputDeviceManager _unifiedInputDeviceInfoInternal;

		/// <summary>
		/// Initializes a new instance with reference to the unified device collection.
		/// </summary>
		/// <param name="unifiedInputDevice">The unified device collection containing all device lists</param>
		public DevicesTab_DeviceSelectedInfo(UnifiedInputDeviceManager unifiedInputDevice)
		{
			_unifiedInputDeviceInfoInternal = unifiedInputDevice ?? throw new ArgumentNullException(nameof(unifiedInputDevice));
		}

		/// <summary>
		/// Gets device information as XAML elements for display.
		/// Extracts properties from the appropriate device list based on input type.
		/// </summary>
		/// <param name="inputType">The input type (DirectInput, RawInput, etc.)</param>
		/// <param name="interfacePath">The device interface path for identification</param>
		/// <returns>UIElement containing formatted device information in 3 columns, or null if device not found</returns>
		public UIElement GetDeviceInformationAsXamlElements(string inputType, string interfacePath)
		{
			if (string.IsNullOrEmpty(inputType) || string.IsNullOrEmpty(interfacePath))
				return null;

			// Get device information list based on input type
			var deviceInfo = GetDeviceInformation(inputType, interfacePath);
			if (deviceInfo == null || deviceInfo.Count == 0)
				return null;

			// Create 3-column layout
			return CreateThreeColumnLayout(deviceInfo);
		}

		/// <summary>
		/// Retrieves device information properties from the appropriate device list.
		/// </summary>
		/// <param name="inputType">The input type identifier</param>
		/// <param name="interfacePath">The device interface path</param>
		/// <returns>List of property name-value pairs</returns>
		private List<(string Name, string Value)> GetDeviceInformation(string inputType, string interfacePath)
		{
			object deviceObject = null;

			switch (inputType)
			{
				case "PnPInput":
					deviceObject = _unifiedInputDeviceInfoInternal.PnPInputDeviceInfoList?
						.FirstOrDefault(d => string.Equals(d.HardwareIds, interfacePath, StringComparison.OrdinalIgnoreCase) ||
											 string.Equals(d.DeviceInstanceId, interfacePath, StringComparison.OrdinalIgnoreCase));
					break;

				case "RawInput":
					deviceObject = _unifiedInputDeviceInfoInternal.RawInputDeviceInfoList?
						.FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
					break;

				case "DirectInput":
					deviceObject = _unifiedInputDeviceInfoInternal.DirectInputDeviceInfoList?
						.FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
					break;

				case "XInput":
					deviceObject = _unifiedInputDeviceInfoInternal.XInputDeviceInfoList?
						.FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
					break;

				case "GamingInput":
					deviceObject = _unifiedInputDeviceInfoInternal.GamingInputDeviceInfoList?
						.FirstOrDefault(d => string.Equals(d.InterfacePath, interfacePath, StringComparison.OrdinalIgnoreCase));
					break;
			}

			if (deviceObject == null)
				return null;

			return ExtractDeviceProperties(deviceObject);
		}

		/// <summary>
		/// Extracts string, GUID, and numeric properties from a device object.
		/// Filters out complex types and null values.
		/// </summary>
		/// <param name="deviceObject">The device object to extract properties from</param>
		/// <returns>List of property name-value pairs</returns>
		private List<(string Name, string Value)> ExtractDeviceProperties(object deviceObject)
		{
			var properties = new List<(string Name, string Value)>();

			if (deviceObject == null)
				return properties;

			var type = deviceObject.GetType();
			var propertyInfos = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in propertyInfos)
			{
				try
				{
					// Only include simple types: string, Guid, and numeric types
					var propType = prop.PropertyType;
					if (!IsDisplayableType(propType))
						continue;

					var value = prop.GetValue(deviceObject);
					if (value == null)
						continue;

					// Format the value appropriately
					string formattedValue = FormatPropertyValue(value, propType);
					if (!string.IsNullOrEmpty(formattedValue))
					{
						properties.Add((prop.Name, formattedValue));
					}
				}
				catch
				{
					// Skip properties that throw exceptions
					continue;
				}
			}

			return properties;
		}

		/// <summary>
		/// Determines if a property type should be displayed.
		/// </summary>
		/// <param name="type">The property type to check</param>
		/// <returns>True if the type should be displayed</returns>
		private bool IsDisplayableType(Type type)
		{
			// Handle nullable types
			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

			return underlyingType == typeof(string) ||
				   underlyingType == typeof(Guid) ||
				   underlyingType == typeof(int) ||
				   underlyingType == typeof(uint) ||
				   underlyingType == typeof(long) ||
				   underlyingType == typeof(ulong) ||
				   underlyingType == typeof(short) ||
				   underlyingType == typeof(ushort) ||
				   underlyingType == typeof(byte) ||
				   underlyingType == typeof(sbyte) ||
				   underlyingType == typeof(double) ||
				   underlyingType == typeof(float) ||
				   underlyingType == typeof(decimal) ||
				   underlyingType == typeof(bool);
		}

		/// <summary>
		/// Formats a property value for display.
		/// </summary>
		/// <param name="value">The value to format</param>
		/// <param name="type">The type of the value</param>
		/// <returns>Formatted string representation</returns>
		private string FormatPropertyValue(object value, Type type)
		{
			if (value == null)
				return string.Empty;

			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

			// Format integers with hex representation if they look like IDs
			if (underlyingType == typeof(int) || underlyingType == typeof(uint))
			{
				var intValue = Convert.ToInt32(value);
				if (intValue > 255) // Show hex for larger values
					return $"{intValue} (0x{intValue:X})";
				return intValue.ToString();
			}

			// Format GUIDs
			if (underlyingType == typeof(Guid))
			{
				return ((Guid)value).ToString();
			}

			// Format booleans
			if (underlyingType == typeof(bool))
			{
				return ((bool)value).ToString();
			}

			// Default string representation
			return value.ToString();
		}

		/// <summary>
		/// Creates a 3-column layout for device properties using UniformGrid.
		/// </summary>
		/// <param name="properties">List of property name-value pairs</param>
		/// <returns>UIElement containing the formatted layout</returns>
		private UIElement CreateThreeColumnLayout(List<(string Name, string Value)> properties)
		{
			if (properties == null || properties.Count == 0)
				return null;

			// Create a UniformGrid with 3 columns for efficient space usage
			var grid = new UniformGrid
			{
				Columns = 3,
				Margin = new Thickness(5)
			};

			foreach (var (name, value) in properties)
			{
				// Create a horizontal StackPanel for each property
				var propertyPanel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(2,0,2,0)
				};

				// Property name label
				var nameLabel = new Label
				{
					Content = $"{name}:",
					FontWeight = FontWeights.Normal,
					Padding = new Thickness(0, 0, 5, 0)
				};

				// Property value label with reduced opacity
				var valueLabel = new Label
				{
					Content = value,
					Opacity = 0.5,
					Padding = new Thickness(0)
				};

				propertyPanel.Children.Add(nameLabel);
				propertyPanel.Children.Add(valueLabel);

				grid.Children.Add(propertyPanel);
			}

			return grid;
		}
	}
}
