﻿using System;
using System.Globalization;
using System.Windows.Data;
using ExplorerEx.Model;
using ExplorerEx.Utils;

namespace ExplorerEx.Converter.Grouping; 

internal class DateTimeGroupingConverter : IValueConverter {
	public static Lazy<DateTimeGroupingConverter> Instance { get; } = new(new DateTimeGroupingConverter());

	private DateTimeGroupingConverter() { }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		var dateTime = (DateTime)value!;
		var now = DateTime.Now;
		if (dateTime > now) {
			return "Today".L();
		}
		if (dateTime.Year == now.Year) {
			if (dateTime.Month == now.Month) {
				return (now.Day - dateTime.Day) switch {
					0 => "Today".L(),
					1 => "Yesterday".L(),
					<= 7 => "ThisWeek".L(),
					<= 14 => "LastWeek".L(),
					_ => "ThisMonth".L()
				};
			}
			if (dateTime.Month == now.Month - 1) {
				return "LastMonth".L();
			}
		}
		return "ALongTimeAgo".L();

		//return offset.TotalHours switch {
		//	< 24d when now.Day == dateTime.Day => new OrderedCollectionViewGroup("Today".L(), 0),
		//	< 24d => new OrderedCollectionViewGroup("Yesterday".L(), 1),
		//	< 7 * 24d => new OrderedCollectionViewGroup("ThisWeek".L(), 2),
		//	< 30 * 24d => new OrderedCollectionViewGroup("EarlierThisMonth".L(), 3),
		//	< 60 * 24d => new OrderedCollectionViewGroup("LastMonth".L(), 4),
		//	_ => new OrderedCollectionViewGroup("ALongTimeAgo".L(), 5)
		//};
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new InvalidOperationException();
	}
}