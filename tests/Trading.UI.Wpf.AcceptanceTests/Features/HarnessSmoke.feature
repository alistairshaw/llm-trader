@stage7 @ui @windows @HarnessSmoke
Feature: WPF automation harness

Scenario: Launch navigate and close the deterministic operator console
  Given the deterministic WPF application is ready
  When I navigate to the Bot Runs workspace
  Then the Bot Runs workspace is displayed
  And the application closes without an orphan or fixture data
