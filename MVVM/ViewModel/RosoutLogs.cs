using Godot;
using MQTTnet;
using RoverControlApp.Core;
using RoverControlApp.MVVM.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RoverControlApp.MVVM.ViewModel
{
	public partial class RosoutLogs : Panel
	{

		private readonly List<MqttClasses.RosoutLogs> _allLogsHistory = new();

		private readonly Dictionary<int, bool> _activeFilters = new()
		{
			//DEBUG
			{ 10, false },
			//INFO
			{ 20, false },
			//WARN
			{ 30, true },
			//ERROR
			{ 40, true },
			//FATAL
			{ 50, true },
		};

		[Export]
		private RichTextLabel _logDisplay = null!;

		public override void _EnterTree()
		{
			MqttNode.Singleton.MessageReceivedAsync += OnRosoutInfo;
		}

		public override void _ExitTree()
		{
			MqttNode.Singleton.MessageReceivedAsync -= OnRosoutInfo;
		}

		public override void _Ready()
		{
			_logDisplay = GetNode<RichTextLabel>("LogsDisplay");
		}

		public Task OnRosoutInfo(string subtopic, MqttApplicationMessage? msg)
		{
			if (string.IsNullOrEmpty(LocalSettings.Singleton.Mqtt.TopicRosoutLogs) || subtopic != LocalSettings.Singleton.Mqtt.TopicRosoutLogs)
				return Task.CompletedTask;
			if (msg == null || msg.PayloadSegment.Count == 0)
			{
				EventLogger.LogMessage("RosoutLogs", EventLogger.LogLevel.Error, "Empty payload");
				return Task.CompletedTask;
			}

			try
			{
				var newLog = JsonSerializer.Deserialize<MqttClasses.RosoutLogs>(msg.ConvertPayloadToString());
				if (newLog == null)
					throw new InvalidDataException("Invalid RosoutLogs payload.");

				lock (_allLogsHistory) 
				{
					_allLogsHistory.Add(newLog);

					if (_allLogsHistory.Count > 1000)
						_allLogsHistory.RemoveAt(0);
				}
				Callable.From(RebuildLogUI).CallDeferred();

			}
			catch (Exception e)
			{
				EventLogger.LogMessage("RosoutLogs", EventLogger.LogLevel.Error, $"{e.Message}");
				
			}
			return Task.CompletedTask;
		}

		private void RebuildLogUI()
		{

			List<MqttClasses.RosoutLogs> logsToRender;

			lock (_allLogsHistory)
			{
				logsToRender = _allLogsHistory
				.Where(log => _activeFilters.TryGetValue(log.level, out bool isActive) && isActive)
				.ToList();
			}

			var sb = new StringBuilder();
			foreach (var log in logsToRender)
			{
				sb.Append($"[color={SetLogColor(log.level)}]{log.Timestamp} : {log.level} : {log.name} : {log.message} : {log.file} :  {log.function} : {log.line}[/color]\n");
			}

			_logDisplay.Text = sb.ToString();
		}

		public void ToggleFilter(bool isPressed, int level)
		{
			if (_activeFilters.ContainsKey(level))
			{
				_activeFilters[level] = isPressed;
				Callable.From(RebuildLogUI).CallDeferred();
			}
		}

		private string SetLogColor(int? level)
		{
			string color = "white";

			switch (level)
			{
				case 10:
					color = "gray";
					break;
				case 20:
					color = "white";
					break;
				case 30:
					color = "yellow";
					break;
				case 40:
				case 50:
					color = "red";
					break;
			}

			return color;
		}
	}
	}
