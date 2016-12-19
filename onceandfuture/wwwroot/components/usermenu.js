import React from 'react';
import { connect } from 'react-redux';
import {
  accountSettingsToggle,
  userMenuToggle,
} from '../actions';
import {
  COLOR_VERY_DARK,
  COLOR_VERY_LIGHT,

  SIZE_BUTTON_HEIGHT,

  Z_INDEX_ACCOUNT_MENU,
} from './style';
import IconButton from './iconbutton';

const Menu = ({visible, onShowSettings}) => {
  const menu_style = {
    display: visible ? 'inline-block' : 'none',
    position: 'absolute',
    top: SIZE_BUTTON_HEIGHT, right: 0,
    width: 120,

    zIndex: Z_INDEX_ACCOUNT_MENU,

    backgroundColor: COLOR_VERY_DARK,
    color: COLOR_VERY_LIGHT,
    textAlign: 'center',
    cursor: 'pointer',
  };

  return <div style={menu_style}>
    <p onClick={onShowSettings}>Account settings...</p>
    <p>Logout</p>
  </div>;
};

const UserMenuBase = function({
  visible,
  onToggle,
  onShowSettings,
}) {
  let style = {};
  if (visible) {
    style = Object.assign({}, style, {
      backgroundColor: COLOR_VERY_DARK,
      color: COLOR_VERY_LIGHT,
    });
  }

  const tip = null; //"View account settings";
  return <div style={style}>
    <IconButton
      tip={tip}
      tipPosition="bottomleft"
      icon="fa-user"
      onClick={onToggle}
    />
    <Menu visible={visible} onShowSettings={onShowSettings} />
  </div>;
};

const mapStateToProps = (state) => {
  return {
    visible: state.user_menu.visible,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onToggle: () => dispatch(userMenuToggle()),
    onShowSettings: () => dispatch(accountSettingsToggle()),
  };
};
const UserMenu = connect(mapStateToProps, mapDispatchToProps)(UserMenuBase);

export default UserMenu;
