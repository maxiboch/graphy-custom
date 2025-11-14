# Changelog - Graphy Custom Fork

## [v0.2.0-custom] - 2025-11-14

### Added
- **FMOD Audio System Monitoring Module**
  - Real-time FMOD CPU usage tracking (DSP, streaming, geometry, update, Studio)
  - Memory allocation monitoring with current and peak values
  - Active audio channels counter
  - File I/O throughput monitoring (KB/s)
  - GC-free implementation using double-ended queues
  - Automatic FMOD system detection and connection
  - Visual graphs for CPU, memory, and channel usage
  - Text displays with current, average, and peak values
  - Color-coded performance thresholds
  - Works without FMOD profiling enabled

### Technical Implementation
- Uses native FMOD API calls via P/Invoke for minimal overhead
- Pre-allocated buffers to avoid runtime allocations
- Leverages existing Graphy double-ended queue for sample storage
- Update rate control to minimize performance impact
- Graceful fallback when FMOD is not available

## [v0.1.0-custom] - 2025-11-14

### Summary
This custom fork integrates multiple open pull requests from the upstream Graphy repository and adds CPU/GPU tracking features from paulatwarp's fork, while using an efficient double-ended queue implementation for performance monitoring.

### Integrated Features

#### From Upstream Graphy PRs:
- **PR #121 - Conditional Audio Module** ([View PR](https://github.com/Tayx94/graphy/pull/121))
  - Author: FurkanKambay
  - Adds ability to conditionally enable/disable the audio monitoring module
  - Useful for platforms where audio monitoring may not be supported or needed

- **PR #122 - Double-ended Queue for Faster Percentile Updates** ([View PR](https://github.com/Tayx94/graphy/pull/122))
  - Author: Paul Sinnett (paulsinnett)
  - Replaces array-based buffer with efficient double-ended queue (deque)
  - Histogram-based percentile calculation - O(n) where n is 1% of sample buffer
  - Improves G_FpsMonitor update from ~0.03ms to <0.01ms per frame
  - More accurate statistical calculations

- **PR #123 - Reset FPS Monitor** ([View PR](https://github.com/Tayx94/graphy/pull/123))
  - Author: Paul Sinnett (paulsinnett)
  - Adds ability to reset the FPS monitor statistics
  - Useful for benchmarking specific sections of gameplay

- **PR #124 - Fix IndexOutOfRangeException** ([View PR](https://github.com/Tayx94/graphy/pull/124))
  - Author: Paul Sinnett (paulsinnett)
  - Fixes potential crash when accessing refresh rate display

#### From paulatwarp's Fork:
- **CPU & GPU Tracking** ([paulatwarp/graphy](https://github.com/paulatwarp/graphy))
  - Commit 82c8cca: Switch from FPS to ms/f display, separate CPU and GPU tracking
  - Commit 227173d: README update documenting the changes
  - Provides separate monitoring for CPU and GPU frame times
  - More precise millisecond per frame display
  - Requires platform support for FrameTimingManager.CaptureFrameTimings()
  - Must be enabled in Unity Player Settings

### Technical Decisions
- **Chose double-ended queue over ring buffer**: The deque implementation from PR #122 was preferred over ring buffer implementations (commits 885fe85 and 518ee59 from paulatwarp) due to:
  - Better performance characteristics
  - Cleaner integration with existing codebase
  - More efficient percentile calculations

### Files Modified
- `Runtime/Fps/G_FpsMonitor.cs` - Core monitoring logic with CPU/GPU tracking and deque
- `Runtime/Fps/G_FpsText.cs` - Display logic for ms/f and CPU/GPU times
- `Runtime/Util/G_DoubleEndedQueue.cs` - Efficient buffer implementation
- `Runtime/Util/G_Histogram.cs` - Statistical calculations
- `Runtime/GraphyManager.cs` - Conditional audio and reset functionality
- `Editor/GraphyManagerEditor.cs` - Editor support for new features
- Various prefabs and UI components

### Compatibility
- Unity 2019.4+ (same as upstream)
- Platforms with FrameTimingManager support for CPU/GPU tracking
- All features from original Graphy are preserved

### Known Limitations
- CPU/GPU tracking requires:
  - Platform support for FrameTimingManager.CaptureFrameTimings()
  - Frame Timing Stats enabled in Player Settings
  - May not work on all mobile devices or WebGL

### Migration from Original Graphy
This fork is designed as a drop-in replacement with additional features. No code changes required unless you want to use the new CPU/GPU tracking features.

### Contributors
- Martin Pane (martinTayx) - Original Graphy author
- Paul Sinnett (paulsinnett) - Deque implementation, reset, bug fixes
- paulatwarp - CPU/GPU tracking, ms/f display
- FurkanKambay - Conditional audio module
- All original Graphy contributors

### License
MIT License (same as original Graphy)