import React from 'react';
import {
  RIVER_TITLE_BACKGROUND_COLOR,

  Z_INDEX_ACCOUNT_SETTINGS,
} from './style';
import { SettingsButton } from './settingscontrols';

const AccountSettings = () => {
  const fontSize = '18px';

  const inner_style = {
    width: 460,
    marginLeft: 'auto',
    marginRight: 'auto',
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    paddingLeft: 5,
    paddingRight: 5,
    border: "2px solid black",
    fontSize: fontSize,

    zIndex: Z_INDEX_ACCOUNT_SETTINGS,
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

  return <div style={inner_style}>
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
  </div>;
};

export default AccountSettings;
