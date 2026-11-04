# Verification report

## Build and startup

- Baseline `dotnet restore` and `dotnet build`: succeeded before implementation.
- Final `dotnet restore` and `dotnet build`: succeeded, zero compiler/analyzer warnings and zero errors.
- `dotnet run --no-build --launch-profile https`
- No application exceptions were observed in the startup/runtime output during final QA. The HTTP-only profile emitted the template's HTTPS-redirection warning because an HTTPS port was not configured for that profile.
- The final clean browser session reported no warning/error console entries. Earlier entries caused by intentionally restarting the development server were excluded from this clean session.
- No Git commits, pushes, package version changes, or added application projects.

## Browser checks actually executed

| Check | Observed result |
| --- | --- |
| Five sidebar destinations | Overview, Floor Plan, Rooms, Trends, and Alarms loaded |
| Floor switching | Each of the three floors displayed its four correct rooms |
| Every SVG room | All twelve room IDs were selected and matched their detail panels |
| Keyboard interaction | Enter on the Conference Room SVG region selected that room |
| Temperature command | Increasing Reception's target from 22.0 to 22.5 °C changed its setpoint while its paused measured temperature stayed unchanged |
| State across pages | Reception's 22.5 °C target appeared on Rooms after changing it on Floor Plan |
| HVAC mode | Cooling could be selected and remained selected after navigation |
| Lighting | Switch off changed effective brightness to 0%, disabled dimming, and updated the SVG; switching on restored brightness |
| Brightness | ArrowRight on the enabled dimmer changed 75% to 80%, reflected in the room and SVG |
| Offline controller | Training Room displayed stale readings and disabled the setpoint buttons, HVAC selector, lighting switch and slider |
| Room directory | Twelve initial cards; Floor 2 filtering returned four rooms |
| Simulation | Header update time changed by four seconds while running; controls remained usable when paused |
| Temperature chart | Measured and setpoint series rendered; changing the selected room changed the chart to Reception |
| Energy chart | Hourly profile, accumulated energy and floor allocations rendered with kW/kWh labels |
| Alarm acknowledgement | ALM-001 gained an acknowledgement timestamp, remained Active, retained the active count of five, and reduced unacknowledged count by one |
| Severity filtering | Critical filter returned two alarm rows plus the table header |
| Alarm search | AHU-02 search returned the communication alarm; a nonexistent source showed the empty state |
| Condition filter | Returned-to-normal filter showed an empty state for the seeded active scenario; All conditions restored five rows |
| Desktop | Inspected at 1440 × 900 and captured actual screenshots |
| Tablet | All five pages measured at 834 × 1112; document width equaled viewport width, with no horizontal overflow |
| Phone | All five pages measured at 390 × 844; document width equaled viewport width, with no horizontal overflow |
| Responsive navigation | Tablet menu opened, sidebar navigation worked, and navigation closed the menu |
| Not-found page | The branded not-found view and return-to-overview link rendered |

Visual review identified and fixed an SVG level-label interpolation issue and the room-card button's inherited horizontal layout. The corrected directory and final floor plan were inspected again. The phone KPI layout was also checked after scoping the Overview-specific card sizing. Historical-series chart sizing and control-panel spacing were also refined during QA.


## Screenshot files

All screenshots are unedited captures of the actual application. Primary captures use a 1440 × 900 browser viewport:

- `screenshots/overview.jpg` — building overview.
- `screenshots/overview-desktop.jpg` — an additional initial overview capture.
- `screenshots/overview-equipment.jpg` — overview scrolled to the equipment section.
- `screenshots/floor-plan.jpg` — Floor 1 with Conference Room selected.
- `screenshots/rooms.jpg` — Floor 2 with the offline Training Room selected.
- `screenshots/alarms.jpg` — alarm register after acknowledging the active Server Room condition.
- `screenshots/trends.jpg` — temperature history and setpoint.
- `screenshots/trends-energy.jpg` — trends page scrolled to energy monitoring.

## Repeatable manual demo

1. Run `dotnet restore`, `dotnet build`, and `dotnet run` from the project directory.
2. Visit Overview; compare twelve rooms, eight occupied spaces, fourteen of sixteen online controllers/devices and five active seeded alarms.
3. Select each floor and room on Floor Plan. Use Tab/Enter as well as the pointer.
4. Pause the simulation; change a setpoint and verify the measured reading stays separate. Resume and observe gradual readings.
5. Switch lighting, adjust brightness and HVAC mode, then navigate to Rooms and confirm settings persist.
6. Select Training Room and verify offline/stale indicators and disabled controls.
7. Choose a room on Trends and compare actual temperature with the target line. Inspect electrical units.
8. Search/filter Alarms. Acknowledge Server Room and verify it stays active.
9. Check the tablet menu and narrow table layout.
