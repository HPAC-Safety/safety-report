# Occurrence report — form specification

> **Generated file — do not edit by hand.**
> Regenerate with `tools/extract-typeform.py`; CI diffs this against the live form.

Source: <https://pq3ivecn4rb.typeform.com/to/ZzIBaNLP>

This is the question set HPAC has been collecting through Typeform. The new
form reproduces it so that historical and future reports stay comparable, and
so reporters see wording they already recognise.

See `docs/anonymization-policy.md` for which of these fields survive into a
published summary — several of them never do.

## Fields

- **Tell us as much as you can about the occurrence** — `statement`
  - We will first ask you 15 short questions about the pilot, location, aircraft, injury, and damage.
We will then ask you to provide a detailed description of the occurrence, and the corrective actions that were taken after the occurrence (along with your recommendations).
You can type in your description or prepare it in advance and copy-paste it. You can leave the form and come back to it. Your answers are saved in your browser for 15 days or until you submit the form.
- **From:** — `contact_info`
  - Tell us who is reporting the occurrence.
  - **First name** — `short_text`
  - **Last name** — `short_text`
  - **Phone number** — `phone_number`
  - **Email** — `email`
- **Pilot:** — `contact_info`
  - Tell us who was the pilot in command.
  - **First name** — `short_text`
  - **Last name** — `short_text`
- **Pilot's ratings:** — `multiple_choice`
  - What are the pilot's most advanced HPAC ratings?
  - Choices (multi-select): `Student`, `P1`, `P2`, `P3`, `P4`, `H1`, `H2`, `H3`, `H4`, `Paragliding Instructor`, `Paragliding Tandem`, `Hang Gliding Instructor`, `Hang Gliding Tandem`
- **Date:** — `date`
  - Tell us the date of the occurrence.
- **Time of day:** — `dropdown`
  - Tell us the time of the occurrence.
  - Choices: `Morning`, `Mid-day`, `Afternoon`, `Evening`, `Do not know`
- **Country:** — `yes_no`
  - Did the occurrence happen in Canada?
- **Where:** — `short_text`
  - Tell us the location of the occurrence.
- **Province:** — `dropdown`
  - Tell us the province of the occurrence.
  - Choices: `Newfoundland and Labrador`, `Prince Edward Island`, `Nova Scotia`, `New Brunswick`, `Quebec`, `Ontario`, `Manitoba`, `Saskatchewan`, `Alberta`, `British Columbia`, `Yukon`, `Northwest Territories`, `Nunavut`
- **Aircraft:** — `group`
  - Tell us more about the aircraft(s) involved in the occurrence.
  - **Type of aircraft:** — `multiple_choice`
    - Tell us the type of aircraft(s) involved in the occurrence.
    - Choices (multi-select): `Paraglider`, `Hang Glider`, `Mini Wing`, `Speedflyer`, `Paraglider Tandem`, `Hang Glider Tandem`
  - **Manufacturer:** — `short_text`
    - Tell us the manufacturer of the pilot's aircraft.
  - **Model:** — `short_text`
    - Tell us about the model of the pilot's aircraft.
  - **Certification:** — `short_text`
    - Tell us about the certification of the pilot's aircraft.
- **Pilot injury:** — `multiple_choice`
  - If any, tell us the type of injury to the pilot.
  - Choices: `No injury`, `Minor injury (no medical aid or on-site aid only)`, `Serious injury (secondary medical aid)`, `Fatality`, `Do not know`
- **Passenger injury:** — `multiple_choice`
  - If any, tell us the type of injury to the passenger.
  - Choices: `No injury`, `Minor injury (no medical aid or on-site aid only)`, `Serious injury (secondary medical aid)`, `Fatality`, `Do not know`
- **Injury description:** — `short_text`
  - If any, describe the injury.
- **Damage:** — `short_text`
  - If any, describe the damage to the aircraft, environment, or property.
- **Description:** — `long_text`
  - Give a precise description of the occurrence. What happened to the aircraft, what did you see, hear, or do? Include your *role* in the occurrence. Think *preflight*, *weather*, *distractions*, *emotions*. Include your thoughts about the *causes* for the incident or accident.
- **Action and prevention:** — `long_text`
  - Tell us what actions were taken after the occurrence. Include your thoughts for prevention.
- **Photo or video:** — `file_upload`
  - Upload one photo or video of the occurrence. Please contact us directly for multiple files (safety@hpac.ca).
- **Short form publication** — `yes_no`
  - Occurrence reports help our entire community. Do you agree for HPAC to publish a de-identified (no name, location, and other identifiable factors) version of your report on its website?
- **Here is a summary of your answers:** — `statement`
  - From: {{field:da89ae06-f229-4f38-8faa-e9c5bafef2f3}} {{field:3d662189-41cb-4430-9db8-7b2e4861df53}}, {{field:65ed41c8-5fbd-4fb0-9ec3-5dabdb42d7be}}, {{field:d4cded5e-1393-47e1-b703-d44e2865ccd8}}
Pilot: {{field:52afac6c-b30c-4bd2-a052-212fa9249a45}} {{field:41c4d104-82c5-4f31-9d86-cb95e35622e4}}
Pilot's Ratings: {{field:db18ab92-b23d-4d21-af63-310f24e1a0bb}}
Date: {{field:c923604b-0aab-4223-92bb-795d77535f57}}
Time of Day: {{field:5d0277fa-67fd-464e-9aef-f419deef7500}}
Where: {{field:56bd800a-2af4-45fd-b4b9-16cbf250bc56}}
Province: {{field:849ea0c4-b36e-44a7-936e-e578967907a3}}
Aircraft: {{field:7590a371-c9cd-4e19-9954-be5137a669d2}}, {{field:7018ad9b-bc9c-4f06-9a36-5fb3eed0368c}}, {{field:762e8ce8-ccd3-4d1f-be09-68eb33136aa5}}, {{field:6879f513-fe00-4bdc-acda-138947650b3c}}
Pilot injury: {{field:a07c69aa-8602-484a-97b0-df4bf6c703c2}}
Passenger injury: {{field:1e24c3db-5bde-464c-af83-e528126050c5}}
Injury description: {{field:d47bfadd-1ea1-40c6-bbdc-9b6084ee941f}}
Damage: {{field:f3051ab2-4225-407a-9ba4-073146bd3fdb}}
Description: {{field:2a27ebe1-5b93-465f-bf67-e3248429d6f8}}
Action and prevention: {{field:d1ba440d-a08a-4398-9481-7b3a36e337f7}}
Photo or video: {{field:418e72ec-1edc-4e0d-9429-af65ca564ab1}}
Publication: {{field:08f3eedb-e682-431d-be9b-2d83765bf022}}
