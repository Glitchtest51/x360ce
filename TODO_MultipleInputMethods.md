# Multiple Input Methods Implementation - x360ce

## Overview
Extend x360ce to support multiple input methods (DirectInput, XInput, Gaming Input, Raw Input) with manual user selection and clear limitation warnings.

## Input Method Limitations Summary

### DirectInput (Method = 0) - Current Implementation
**LIMITATIONS:**
- ⚠️ **Xbox One controllers CANNOT be accessed in the background on Windows 10+**
- ⚠️ **Xbox 360/One controllers have triggers on the same axis** (no separate LT/RT)
- ⚠️ **No Guide button access** through DirectInput
- ⚠️ **No rumble/force feedback** for Xbox controllers via DirectInput
- ⚠️ **Windows Store Apps can't use DirectInput**
- ⚠️ **Microsoft no longer recommends using DirectInput** (deprecated)

**CAPABILITIES:**
- ✅ All controller types supported
- ✅ Unlimited device count
- ✅ Generic controllers work perfectly

### XInput (Method = 1) - New Implementation
**LIMITATIONS:**
- ❌ **Maximum 4 controllers ONLY** (hard XInput API limit)
- ❌ **Only XInput capable devices** (no generic gamepads)
- ❌ **Cannot activate extra 2 rumble motors** in Xbox One controller triggers

**CAPABILITIES:**
- ✅ **XInput controllers can be accessed in the background**
- ✅ **Proper trigger separation** (LT/RT as separate axes)
- ✅ **Guide button access** available
- ✅ **Full rumble support** available

### Gaming Input (Method = 2) - New Implementation
**LIMITATIONS:**
- ⚠️ **Controllers CANNOT be accessed in the background** (UWP limitation)
- ❌ **Only works on UWP devices** (Windows 10+, Xbox One, tablets)
- ❌ **Desktop apps need special WinRT bridging** (complex implementation)

**CAPABILITIES:**
- ✅ **Unlimited(?) number of controllers** on Windows 10
- ✅ **Gamepad class**: Xbox One certified/Xbox 360 compatible only
- ✅ **RawGameController class**: Other gamepads supported
- ✅ **Full Xbox One controller features** (including trigger rumble)

### Raw Input (Method = 3) - New Implementation
**LIMITATIONS:**
- ⚠️ **Xbox 360/One controllers have triggers on same axis** (same as DirectInput)
- ⚠️ **No Guide button access**
- ⚠️ **Probably no rumble** (needs verification)
- ❌ **Requires manual HID report parsing** (complex implementation)
- ❌ **No built-in controller abstraction** (custom profiles needed)

**CAPABILITIES:**
- ✅ **Controllers CAN be accessed in the background**
- ✅ **Unlimited number of controllers**
- ✅ **Works with any HID-compliant device**

---

## Implementation Tasks

### Phase 1: Core Infrastructure ✅ **COMPLETED**

#### 1.1 Add InputMethod Enum ✅ **DONE**
- [x] Create `InputMethod` enum in x360ce.Engine
- [x] Add to `x360ce.Engine/Common/InputMethod.cs`
- [x] Values: DirectInput=0, XInput=1, GamingInput=2, RawInput=3

#### 1.2 Extend UserDevice Class ✅ **DONE**
- [x] Add `InputMethod` property to `UserDevice` class
- [x] Add default value = `InputMethod.DirectInput` (backward compatibility)
- [x] Update serialization/deserialization logic
- [x] Add validation methods
- [x] Add Xbox controller detection (`IsXboxCompatible`)

#### 1.3 Create Input Processor Interface ✅ **DONE**
- [x] Create `x360ce.App/Common/DInput/IInputProcessor.cs`
- [x] Define common interface for all input methods
- [x] Include methods: `CanProcess()`, `ReadState()`, `HandleForceFeedback()`
- [x] Add `ValidationResult` and `InputMethodException` classes

#### 1.4 Update DInputHelper Architecture ✅ **DONE**
- [x] Modify `DInputHelper.Step2.UpdateDiStates.cs` to dispatch by InputMethod
- [x] Create processor factory pattern
- [x] Add error handling without fallbacks
- [x] Maintain backward compatibility

### Phase 1A: File Organization ✅ **COMPLETED**

#### 1A.1 Extract Input Processor Registry ✅ **DONE**
- [x] Create `x360ce.App/Common/DInput/DInputHelper.Step2.InputProcessor.cs`
- [x] Extract processor registry and common interface logic
- [x] Move processor factory methods
- [x] Extract validation helpers

#### 1A.2 Extract XInput Processing Logic ✅ **DONE**
- [x] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateXiStates.cs`
- [x] Extract XInput-specific processing from main UpdateDiStates
- [x] Move XInput validation and error handling
- [x] Maintain integration with existing processor architecture

#### 1A.3 Refactor Main DirectInput File ✅ **DONE**
- [x] Keep DirectInput logic and main coordinator in `UpdateDiStates.cs`
- [x] Remove XInput-specific code (moved to UpdateXiStates)
- [x] Remove processor registry code (moved to InputProcessor)
- [x] Keep hybrid approach (DirectInput legacy + processor dispatch)

#### 1A.4 Create Placeholder Files ✅ **DONE**
- [x] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateGiStates.cs` (Gaming Input)
- [x] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateRiStates.cs` (Raw Input)
- [x] Add placeholder classes implementing IInputProcessor
- [x] Document requirements and limitations for each method

### Phase 2: DirectInput Processor ✅ **COMPLETED**

#### 2.1 Refactor Existing DirectInput Code ✅ **DONE**
- [x] Rename current `UpdateDiStates` to `UpdateDiStates` (no change needed)
- [x] Add comprehensive limitation documentation
- [x] Create `DirectInputProcessor` class implementing `IInputProcessor`
- [x] Extract DirectInput-specific logic from main DInputHelper

#### 2.2 Add DirectInput Limitations Documentation ✅ **DONE**
- [x] Add XML documentation with all limitations
- [x] Document Xbox controller background access issue
- [x] Document trigger axis combination limitation
- [x] Document missing Guide button and rumble

### Phase 3: XInput Processor ✅ **COMPLETED**

#### 3.1 Create XInput State Reader ✅ **DONE**
- [x] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateXiStates.cs`
- [x] Implement XInput controller detection
- [x] Add XInput state reading using existing SharpDX.XInput
- [x] Handle 4-controller limitation

#### 3.2 XInput to CustomDiState Mapping ✅ **DONE**
- [x] Map XInput Gamepad buttons to CustomDiState.Buttons[0-15]
- [x] Map triggers to separate CustomDiState.Axis entries
- [x] Map thumbsticks to CustomDiState.Axis entries
- [x] Map D-Pad to CustomDiState POV or separate buttons

#### 3.3 XInput Force Feedback ✅ **DONE**
- [x] Implement XInput vibration through existing XInput API
- [x] Map to existing ForceFeedbackState system
- [x] Handle background access advantage

#### 3.4 XInput Validation ✅ **DONE**
- [x] Implement Xbox controller detection
- [x] Validate maximum 4 controller limit
- [x] Add user warnings for non-Xbox controllers

### Phase 4: Gaming Input Processor (Priority: Low)

#### 4.1 Add Gaming Input Dependencies
- [ ] Add Windows.Gaming.Input NuGet package references
- [ ] Handle Windows 10+ version detection
- [ ] Add UWP bridging for desktop applications

#### 4.2 Create Gaming Input State Reader
- [ ] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateGiStates.cs`
- [ ] Implement Gamepad class usage for Xbox controllers
- [ ] Implement RawGameController for generic controllers
- [ ] Handle background access limitation

#### 4.3 Gaming Input to CustomDiState Mapping
- [ ] Map Gamepad properties to CustomDiState
- [ ] Handle trigger rumble features
- [ ] Map RawGameController for generic devices

#### 4.4 Gaming Input Validation
- [ ] Check Windows 10+ requirement
- [ ] Detect UWP vs desktop application context
- [ ] Warn about background access limitation

### Phase 5: Raw Input Processor (Priority: Low)

#### 5.1 Create Raw Input Infrastructure
- [ ] Create `x360ce.App/Common/DInput/DInputHelper.Step2.UpdateRiStates.cs`
- [ ] Implement Windows Raw Input API integration
- [ ] Add HID report descriptor parsing

#### 5.2 HID Report Processing
- [ ] Create HID report parser for common controller types
- [ ] Map HID usage tables to CustomDiState
- [ ] Handle device capability detection

#### 5.3 Raw Input Device Profiles
- [ ] Create configuration system for unknown devices
- [ ] Add UI for custom button/axis mapping
- [ ] Handle Xbox controller HID reports

#### 5.4 Raw Input Validation
- [ ] Detect HID-compliant devices
- [ ] Handle device capability limitations
- [ ] Document complex setup requirements

### Phase 6: User Interface ✅ **COMPLETED**

#### 6.1 Device Configuration UI ✅ **DONE**
- [x] Add InputMethod dropdown to device configuration
- [x] Display method-specific limitations
- [x] Add validation with clear error messages
- [x] No automatic fallback - user must choose

#### 6.2 Method Selection Dropdown ✅ **DONE**
- [x] Create InputMethod dropdown with descriptions:
  - "DirectInput - All controllers ⚠️ Xbox background issue"
  - "XInput - Xbox only (Max 4) ✅ Background OK"
  - "Gaming Input - Win10+ ⚠️ No background"
  - "Raw Input - All controllers ✅ Background OK"

#### 6.3 Status Indicators ✅ **DONE**
- [x] Show current method status for each device
- [x] Display warnings when limitations are encountered
- [x] Show controller count for XInput (X/4 used)

#### 6.4 Error Messages ✅ **DONE**
- [x] Create clear error messages for each limitation
- [x] No fallback suggestions - let user choose
- [x] Link to documentation explaining limitations

### Phase 7: Validation and Error Handling ✅ **COMPLETED**

#### 7.1 Method Validation System ✅ **DONE**
- [x] Create `ValidationResult` class
- [x] Implement validation for each input method
- [x] Handle method-specific limitations gracefully
- [x] No automatic fallbacks

#### 7.2 Error Handling Strategy ✅ **DONE**
- [x] Log input method failures clearly
- [x] Show user-friendly error messages
- [x] Clear device state on method failure
- [x] Maintain UI responsiveness during errors

#### 7.3 Compatibility Checking ✅ **DONE**
- [x] Check Windows version for Gaming Input
- [x] Detect Xbox controller compatibility for XInput
- [x] Validate controller count limits
- [x] Test HID compliance for Raw Input

### Phase 8: Documentation and Testing (Priority: Medium)

#### 8.1 Code Documentation
- [ ] Add comprehensive XML documentation to all new classes
- [ ] Document limitations in each processor
- [ ] Create usage examples
- [ ] Update existing documentation

#### 8.2 User Documentation
- [ ] Update Help.rtf with input method information
- [ ] Create troubleshooting guide for each method
- [ ] Document when to use each method
- [ ] Add limitation explanations

#### 8.3 Testing Strategy
- [ ] Test each input method with different controller types
- [ ] Verify background access behavior
- [ ] Test controller count limits
- [ ] Validate CustomDiState mapping consistency

#### 8.4 Regression Testing
- [ ] Ensure backward compatibility with existing configurations
- [ ] Test DirectInput behavior unchanged
- [ ] Verify existing force feedback works
- [ ] Test with existing controller configurations

---

## Implementation Notes

### Code Reuse Strategy
- Maximize reuse of existing `CustomDiState` mapping logic
- Share common controller detection between methods
- Reuse force feedback infrastructure where possible
- Maintain consistent coordinate system conversions

### Error Handling Philosophy
- **No automatic fallbacks** - user must manually select method
- Clear error messages explaining limitations
- Graceful failure without crashing
- Preserve user configuration choices

### UI Design Principles
- Clear limitation warnings for each method
- Educational tooltips explaining when to use each method
- Status indicators showing current method effectiveness
- No hidden automatic behavior

### Performance Considerations
- Minimize overhead when switching between methods
- Cache method capabilities to avoid repeated checks
- Efficient state conversion between input APIs
- Maintain existing polling performance

---

## Priority Summary

1. **High Priority**: Infrastructure, XInput, UI, Validation
2. **Medium Priority**: DirectInput refactoring, Documentation
3. **Low Priority**: Gaming Input, Raw Input (complex implementations)

## Success Criteria

- [ ] User can manually select input method for each device
- [ ] Clear warnings shown for each method's limitations
- [ ] No automatic fallbacks - user maintains control
- [ ] Backward compatibility with existing DirectInput configurations
- [ ] XInput provides background access for Xbox controllers
- [ ] All methods produce consistent CustomDiState output
- [ ] Comprehensive error handling without crashes
- [ ] Clear documentation of all limitations
