var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNSPACER,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import { SettingsButton } from './settingscontrols';

const AccountSettings = () => {
  const outer_style = {
    position: 'absolute',
    top: 0,
    left: 0,

    width: '100%',
    height: '100%',
  };

  const fontSize = '18px';

  const inner_style = {
    marginLeft: 'auto',
    marginRight: 'auto',
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    width: 460,
    height: 300,
    marginTop: 33 + COLUMNSPACER,
    paddingLeft: 5,
    paddingRight: 5,
    border: "2px solid black",
    fontSize: fontSize,
  };

  const in_style = {
    width: 215,
    fontSize: fontSize,
  };

  const p_style = {
    width: 215,
    display: 'inline-block',
  };

  const p_l_style = Object.assign({}, p_style, {
    textAlign: 'right',
    paddingRight: 10,
  });
  const p_r_style = Object.assign({}, p_style, {
    paddingLeft: 10,
  });

  const r_style = {
    marginTop: 10,
  };

  const r_b_style = {
    marginTop: 15,
    marginBottom: 25,
    paddingRight: 5,
  };

  return <div style={outer_style}>
    <div style={inner_style}>
      <h2>Account Settings</h2>
      <hr />
      <div style={r_style}>
        <div style={p_l_style}>Email Address:</div>
        <div style={p_r_style}>
          <input style={in_style} type="text" id="email" name="email" placeholder="Email Address" />
        </div>
      </div>
      <div style={r_b_style}>
        <SettingsButton text="Change Email Address" />
      </div>
      <div style={r_style}>
        <div style={p_l_style}>Password:</div>
        <div style={p_r_style}>
          <input style={in_style} type="password" id="pw1" name="pw1" placeholder="Password" />
        </div>
      </div>
      <div style={r_style}>
        <div style={p_l_style}>Repeat Password:</div>
        <div style={p_r_style}>
          <input style={in_style} type="password" id="pw2" name="pw2" placeholder="Password" />
        </div>
      </div>
      <div style={r_b_style}>
        <SettingsButton text="Change Password" />
      </div>
    </div>
  </div>;
};

export default AccountSettings;
